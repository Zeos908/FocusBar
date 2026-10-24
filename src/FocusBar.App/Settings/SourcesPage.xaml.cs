using System.IO;
using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Services;
using FocusBar.App.Sources;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Sources;
using FocusBar.Core.Sources.School;
using Microsoft.Win32;

namespace FocusBar.App.Settings;

/// <summary>Settings → Deadline sources: the school website (sign in, demo data, disconnect) and calendars.</summary>
public partial class SourcesPage : UserControl
{
    private readonly SyncService _sync;
    private readonly ErrorLog _log;

    internal SourcesPage(SyncService sync, ErrorLog log)
    {
        _sync = sync;
        _log = log;
        InitializeComponent();

        HostBox.Text = sync.Session?.Host ?? sync.SchoolConfig.Host;
        sync.StatusChanged += OnStatusChanged;
        Unloaded += (_, _) => sync.StatusChanged -= OnStatusChanged;
        Refresh();
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        SyncButton.IsEnabled = !_sync.IsSyncing;
        SyncButtonText.Text = _sync.IsSyncing ? "Syncing…" : "Sync now";
        LastSyncText.Text = _sync.LastSync is { } last
            ? $"FocusBar checks these every 30 minutes. Last checked {Ago(last)}."
            : "FocusBar checks these every 30 minutes.";

        RefreshSchool();
        FeedList.ItemsSource = _sync.Feeds.Select(feed => new FeedRow(feed, _sync.StatusOf(feed.Key))).ToList();
    }

    private void RefreshSchool()
    {
        var config = _sync.SchoolConfig;
        string text;
        string dot;

        switch (config.Mode)
        {
            case SchoolMode.Demo:
                var demo = _sync.StatusOf(MockSchoolApiSource.SourceKey);
                text = "Showing demo data (made-up assignments and schedule).";
                dot = demo?.Error is null ? "UrgencyMedium" : "UrgencyHigh";
                break;

            case SchoolMode.Live when _sync.Session is { } session:
                var status = _sync.StatusOf(SchoolApiSource.SourceKey);
                var who = string.IsNullOrWhiteSpace(session.DisplayName) ? "" : $" as {session.DisplayName}";
                if (status?.Error is { } error)
                {
                    text = status.NeedsSignIn ? $"Sign-in expired for {session.Host}. Sign in again to keep syncing." : $"Connected{who}, but the last sync failed: {error}";
                    dot = "UrgencyHigh";
                }
                else
                {
                    text = status?.LastSuccess is { } ok ? $"Connected{who} to {session.Host}. Synced {Ago(ok)}." : $"Connected{who} to {session.Host}.";
                    dot = "UrgencyLow";
                }
                break;

            default:
                text = "Not connected.";
                dot = "FlyoutMutedForeground";
                break;
        }

        SchoolStatusText.Text = text;
        SchoolDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, dot);
        SignInButton.Content = config.Mode == SchoolMode.Live && _sync.Session is not null ? "Sign in again…" : "Sign in…";
        DemoButton.IsEnabled = config.Mode != SchoolMode.Demo;
        DisconnectButton.IsEnabled = config.Mode != SchoolMode.Off;
    }

    private static string Ago(DateTimeOffset moment)
    {
        var elapsed = DateTimeOffset.Now - moment;
        if (elapsed < TimeSpan.FromMinutes(1)) return "just now";
        return Countdown.Describe(moment, DateTimeOffset.Now);
    }

    private string? ValidHost()
    {
        var host = SchoolApiConfig.CleanHost(HostBox.Text);
        var valid = SchoolApiConfig.IsValidHost(host);
        HostError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        if (valid) HostBox.Text = host;
        return valid ? host : null;
    }

    private async void Sync_Click(object sender, RoutedEventArgs e) => await _sync.SyncNowAsync();

    private void SignIn_Click(object sender, RoutedEventArgs e)
    {
        if (ValidHost() is not { } host) return;
        var window = new SchoolSignInWindow(host, _sync, _log) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        Refresh();
    }

    private void PasteCookie_Click(object sender, RoutedEventArgs e)
    {
        if (ValidHost() is not { } host) return;
        new PasteCookieDialog(host, _sync, _log, Window.GetWindow(this)!).ShowDialog();
        Refresh();
    }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        if (_sync.SchoolConfig.Mode == SchoolMode.Live && !Confirm("Switch to demo data? This signs out of your school account on FocusBar.")) return;
        if (_sync.SchoolConfig.Mode == SchoolMode.Live) SchoolSignInWindow.DeleteBrowserData();
        _sync.UseDemoData();
        Refresh();
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm("Disconnect the school website? FocusBar removes the school's deadlines and schedule and forgets your sign-in.")) return;
        _sync.DisconnectSchool();
        SchoolSignInWindow.DeleteBrowserData();
        Refresh();
    }

    private bool Confirm(string question) =>
        MessageBox.Show(Window.GetWindow(this)!, question, "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a calendar file",
            Filter = "Calendar files (*.ics)|*.ics|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        FeedLocationBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(FeedNameBox.Text)) FeedNameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void AddFeed_Click(object sender, RoutedEventArgs e)
    {
        var name = FeedNameBox.Text.Trim();
        var location = FeedLocationBox.Text.Trim().Trim('"');
        var className = string.IsNullOrWhiteSpace(FeedClassBox.Text) ? null : FeedClassBox.Text.Trim();

        string? problem = null;
        if (name.Length == 0) problem = "Give the calendar a name.";
        else if (location.Length == 0) problem = "Paste a calendar link or choose an .ics file.";
        else
        {
            var feed = new CalendarFeed(name, location, className);
            if (!feed.IsUrl && !File.Exists(location)) problem = "That file doesn't exist. Use Browse… to pick it.";
            else if (feed.IsUrl && !Uri.TryCreate(feed.Location.Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out _))
                problem = "That link doesn't look right. It should start with https:// or webcal://.";
            else if (!_sync.AddCalendar(feed)) problem = "That calendar is already added.";
        }

        FeedError.Text = problem ?? "";
        FeedError.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        if (problem is not null) return;

        FeedNameBox.Clear();
        FeedLocationBox.Clear();
        FeedClassBox.Clear();
        Refresh();
    }

    private void RemoveFeed_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not FeedRow row) return;
        if (!Confirm($"Remove \"{row.Name}\"? Its deadlines are removed from FocusBar too.")) return;
        _sync.RemoveCalendar(row.Key);
        Refresh();
    }

    /// <summary>One calendar in the list. Links show only their site, since private links work like passwords.</summary>
    private sealed class FeedRow(CalendarFeed feed, SourceStatus? status)
    {
        public string Key => feed.Key;
        public string Name => feed.Name;
        public string RemoveLabel => $"Remove {feed.Name}";

        public string Detail
        {
            get
            {
                var where = feed.IsUrl && Uri.TryCreate(feed.Location.Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out var uri)
                    ? $"Link on {uri.Host}"
                    : Path.GetFileName(feed.Location);
                var state = status switch
                {
                    { Error: { } error } => $"Problem: {error}",
                    { LastSuccess: { } ok } => $"Synced {Ago(ok)}",
                    _ => "Waiting to sync",
                };
                return $"{where} · {state}";
            }
        }
    }
}
