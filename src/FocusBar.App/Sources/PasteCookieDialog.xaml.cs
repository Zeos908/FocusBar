using System.Windows;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Sources.School;

namespace FocusBar.App.Sources;

/// <summary>
/// Fallback sign-in: the student copies the Cookie header from their own browser's developer tools
/// (the method in their API guide) and pastes it here.
/// </summary>
public partial class PasteCookieDialog : Window
{
    private readonly string _host;
    private readonly SyncService _sync;
    private readonly ErrorLog _log;

    internal PasteCookieDialog(string host, SyncService sync, ErrorLog log, Window owner)
    {
        _host = host;
        _sync = sync;
        _log = log;
        Owner = owner;
        InitializeComponent();

        StepsText.Text =
            $"1. In Chrome or Edge, sign in to https://{host}/ as usual.\n" +
            "2. Press F12 to open developer tools, then pick the Network tab.\n" +
            "3. Refresh the page and click any request to the school site.\n" +
            "4. Under Request Headers, find \"cookie:\" and copy its whole value.\n" +
            "5. Paste it below.";

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Loaded += (_, _) => CookieBox.Focus();
    }

    public SchoolContext? Result { get; private set; }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ConnectButton.IsEnabled = false;
        ConnectButton.Content = "Checking…";
        try
        {
            Result = await _sync.ConnectSchoolAsync(_host, CookieBox.Text, CancellationToken.None);
            DialogResult = true;
        }
        catch (SchoolSignInException ex)
        {
            ShowError(ex.Message.Contains("doesn't look like", StringComparison.Ordinal)
                ? ex.Message
                : "That cookie didn't work. It may have expired: sign in again in your browser and copy a fresh one.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write(ex, "Connecting with a pasted cookie failed.");
            ShowError("Couldn't reach the school website. Check your internet connection and try again.");
        }
        finally
        {
            ConnectButton.IsEnabled = true;
            ConnectButton.Content = "Connect";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
