using System.Net;
using System.Text;
using System.Text.Json;

namespace FocusBar.Core.Sources.School;

/// <summary>
/// Talks to a Blackbaud onCampus site (*.myschoolapp.com) the way its own website does, reusing the student's
/// signed-in session cookies. Read-only: FocusBar only GETs, so it never needs the site's CSRF token.
/// Based on the student's <c>cranbrook.py</c> client and API guide.
/// <list type="bullet">
/// <item>Sends <c>X-Requested-With: XMLHttpRequest</c> (some routes require it).</item>
/// <item>Keeps cookies the site updates (it rotates its session token) so the session stays valid.</item>
/// <item>Retries a 403 once, since the site occasionally rejects a request while rotating its token.</item>
/// <item>Waits a little between requests so it never hammers the school's server.</item>
/// </list>
/// </summary>
public sealed class MySchoolAppClient : IDisposable
{
    public static readonly TimeSpan MinimumGap = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _http;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private readonly Dictionary<string, string> _cookies;
    private DateTime _lastRequest = DateTime.MinValue;

    /// <param name="handler">For tests; normally null.</param>
    /// <param name="delay">For tests; normally <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public MySchoolAppClient(string host, string cookieHeader, HttpMessageHandler? handler = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        Host = host;
        _cookies = ParseCookieHeader(cookieHeader);
        _delay = delay ?? Task.Delay;
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            UseCookies = false, // cookies are managed here so they can be saved (encrypted) between runs
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            BaseAddress = new Uri($"https://{host}/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _http.DefaultRequestHeaders.Add("User-Agent", "FocusBar/0.1 (student project; read-only)");
    }

    public string Host { get; }

    /// <summary>True when the site changed a cookie, so the caller should save <see cref="CookieHeader"/>.</summary>
    public bool CookiesChanged { get; private set; }

    /// <summary>The current cookies in Cookie-header form ("a=1; b=2").</summary>
    public string CookieHeader
    {
        get
        {
            lock (_cookies) return string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}"));
        }
    }

    public bool HasCookies
    {
        get
        {
            lock (_cookies) return _cookies.Count > 0;
        }
    }

    public void MarkCookiesSaved() => CookiesChanged = false;

    /// <summary>GETs a JSON endpoint, e.g. <c>/api/webapp/context/</c>, with query parameters.</summary>
    public async Task<JsonElement> GetJsonAsync(string path, IReadOnlyDictionary<string, string>? query, CancellationToken ct)
    {
        var url = BuildUrl(path, query);

        await _oneAtATime.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                await WaitForGapAsync(ct).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var cookieHeader = CookieHeader;
                if (cookieHeader.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                _lastRequest = DateTime.UtcNow;
                UpdateCookies(response);

                if (response.StatusCode == HttpStatusCode.Forbidden && attempt == 1)
                {
                    await _delay(RetryDelay, ct).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new SchoolSignInException(
                        "The school website says you're not signed in. Your sign-in has probably expired.", response.StatusCode);
                }

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new SchoolApiException($"The school website returned an error ({(int)response.StatusCode}).", response.StatusCode);
                }

                return ParseJson(body);
            }
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    /// <summary>
    /// When the session has expired, the site sends its sign-in web page instead of data. Treat that as signed out.
    /// </summary>
    private static JsonElement ParseJson(string body)
    {
        var text = body.TrimStart();
        if (text.StartsWith('<')) throw new SchoolSignInException("The school website sent its sign-in page. Please sign in again.");

        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new SchoolApiException("The school website sent something FocusBar couldn't read.", inner: ex);
        }
    }

    private async Task WaitForGapAsync(CancellationToken ct)
    {
        var wait = _lastRequest + MinimumGap - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await _delay(wait, ct).ConfigureAwait(false);
    }

    internal static string BuildUrl(string path, IReadOnlyDictionary<string, string>? query)
    {
        var builder = new StringBuilder(path.TrimStart('/'));
        if (query is { Count: > 0 })
        {
            builder.Append(path.Contains('?') ? '&' : '?');
            builder.AppendJoin('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        }
        return builder.ToString();
    }

    private void UpdateCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        lock (_cookies)
        {
            foreach (var header in values)
            {
                var parts = header.Split(';');
                var nameValue = parts[0];
                var eq = nameValue.IndexOf('=');
                if (eq <= 0) continue;

                var name = nameValue[..eq].Trim();
                var value = nameValue[(eq + 1)..].Trim();
                if (IsDeletion(parts.Skip(1), value))
                {
                    CookiesChanged |= _cookies.Remove(name);
                }
                else if (!_cookies.TryGetValue(name, out var old) || old != value)
                {
                    _cookies[name] = value;
                    CookiesChanged = true;
                }
            }
        }
    }

    private static bool IsDeletion(IEnumerable<string> attributes, string value)
    {
        if (value.Length == 0) return true;
        foreach (var attribute in attributes)
        {
            var eq = attribute.IndexOf('=');
            if (eq < 0) continue;
            var key = attribute[..eq].Trim();
            var val = attribute[(eq + 1)..].Trim();
            if (key.Equals("Max-Age", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var age) && age <= 0) return true;
            if (key.Equals("Expires", StringComparison.OrdinalIgnoreCase) && DateTimeOffset.TryParse(val, out var expires) && expires < DateTimeOffset.UtcNow) return true;
        }
        return false;
    }

    /// <summary>
    /// Reads a Cookie header copied from the browser ("t=...; AuthSvcToken=...; ..."). Also accepts it with a
    /// leading "cookie:" and with line breaks, as it often comes when copied out of DevTools.
    /// </summary>
    public static Dictionary<string, string> ParseCookieHeader(string header)
    {
        var text = header.Replace("\r", "").Replace("\n", "").Trim();
        if (text.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase)) text = text["cookie:".Length..];

        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in text.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (name.Length > 0 && !name.Any(char.IsWhiteSpace)) cookies[name] = value;
        }
        return cookies;
    }

    public void Dispose()
    {
        _http.Dispose();
        _oneAtATime.Dispose();
    }
}
