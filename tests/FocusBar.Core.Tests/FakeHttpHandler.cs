using System.Net;
using System.Text;

namespace FocusBar.Core.Tests;

/// <summary>Answers HTTP requests in tests and records what was asked, without touching the network.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(), request.RequestUri.AbsolutePath, headers));
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Json(string json, params string[] setCookies)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var cookie in setCookies) response.Headers.Add("Set-Cookie", cookie);
        return response;
    }

    public static HttpResponseMessage Text(string text, string mediaType) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("") };
}

internal sealed record RecordedRequest(HttpMethod Method, string Url, string Path, Dictionary<string, string> Headers)
{
    public string Query(string name)
    {
        var query = new Uri(Url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name) return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        }
        throw new KeyNotFoundException($"No query parameter '{name}' in {Url}");
    }
}
