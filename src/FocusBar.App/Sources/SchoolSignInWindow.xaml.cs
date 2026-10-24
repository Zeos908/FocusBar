using System.IO;
using System.Windows;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Sources.School;
using Microsoft.Web.WebView2.Core;

namespace FocusBar.App.Sources;

/// <summary>
/// Shows the school website in an embedded Edge browser (WebView2) so the student signs in exactly as usual
/// (including Blackbaud ID / single sign-on). After each page load FocusBar reads the site's session cookies
/// and checks them; once they work, they're saved encrypted and the window closes.
/// This replaces copying the cookie out of the browser's developer tools by hand (still available as a fallback).
/// </summary>
public partial class SchoolSignInWindow : Window
{
    private static readonly TimeSpan MinimumCheckGap = TimeSpan.FromSeconds(2);

    private readonly string _host;
    private readonly SyncService _sync;
    private readonly ErrorLog _log;
    private bool _checking;
    private bool _connected;
    private DateTime _lastCheck = DateTime.MinValue;

    internal SchoolSignInWindow(string host, SyncService sync, ErrorLog log)
    {
        _host = host;
        _sync = sync;
        _log = log;
        InitializeComponent();

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Loaded += async (_, _) => await StartBrowserAsync();
    }

    /// <summary>Who signed in, once it worked.</summary>
    public SchoolContext? Result { get; private set; }

    /// <summary>
    /// The browser's own profile (cookies, cache) lives in FocusBar's local app data, not next to the program,
    /// which may not be writable.
    /// </summary>
    internal static string BrowserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusBar", "WebView2");

    private async Task StartBrowserAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, BrowserDataFolder);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.NavigationCompleted += async (_, _) => await CheckSignInAsync(automatic: true);
            Browser.CoreWebView2.Navigate($"https://{_host}/");
            LoadingText.Visibility = Visibility.Collapsed;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowProblem("This PC is missing the Microsoft Edge WebView2 Runtime, which the sign-in window needs. " +
                        "Install it from Microsoft (search \"WebView2 Runtime\"), or close this window and use \"Paste session cookie\" instead.");
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not start the sign-in browser.");
            ShowProblem("The sign-in window couldn't start. Close it and use \"Paste session cookie\" instead.");
        }
    }

    private void ShowProblem(string message)
    {
        LoadingText.Text = message;
        LoadingText.TextWrapping = TextWrapping.Wrap;
        LoadingText.MaxWidth = 520;
        LoadingText.Visibility = Visibility.Visible;
        DoneButton.IsEnabled = false;
    }

    private async void DoneButton_Click(object sender, RoutedEventArgs e) => await CheckSignInAsync(automatic: false);

    /// <summary>Reads the site's cookies from the browser and tries them against the school website.</summary>
    private async Task CheckSignInAsync(bool automatic)
    {
        if (_checking || _connected || Browser.CoreWebView2 is null) return;
        if (automatic)
        {
            // Only once the browser is back on the school's own site, and not on every redirect of the sign-in flow.
            if (!Uri.TryCreate(Browser.CoreWebView2.Source, UriKind.Absolute, out var page) ||
                !page.Host.Equals(_host, StringComparison.OrdinalIgnoreCase) ||
                DateTime.UtcNow - _lastCheck < MinimumCheckGap)
            {
                return;
            }
        }

        _checking = true;
        _lastCheck = DateTime.UtcNow;
        try
        {
            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync($"https://{_host}/");
            var header = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            if (header.Length == 0)
            {
                if (!automatic) StatusText.Text = "Not signed in yet. Finish signing in, then try again.";
                return;
            }

            StatusText.Text = "Checking your sign-in…";
            Result = await _sync.ConnectSchoolAsync(_host, header, CancellationToken.None);
            _connected = true;
            if (IsVisible) DialogResult = true; // the student may have closed the window while it was checking
        }
        catch (SchoolSignInException)
        {
            StatusText.Text = automatic
                ? "Sign in to your school website as you normally do."
                : "FocusBar can't see a signed-in session yet. Finish signing in, then click \"I'm signed in\".";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write(ex, "Checking the school sign-in failed.");
            StatusText.Text = "Couldn't check your sign-in. Check your internet connection and try again.";
        }
        finally
        {
            _checking = false;
        }
    }

    /// <summary>Removes the sign-in browser's saved cookies and cache (used when disconnecting the school).</summary>
    internal static void DeleteBrowserData()
    {
        try
        {
            if (Directory.Exists(BrowserDataFolder)) Directory.Delete(BrowserDataFolder, recursive: true);
        }
        catch (IOException)
        {
            // Still in use; it holds nothing FocusBar relies on.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
