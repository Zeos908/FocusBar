using System.Globalization;
using System.Windows;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.Core.Focus;
using FocusBar.Core.Models;
using FocusBar.Core.Reminders;

namespace FocusBar.App.Focus;

/// <summary>Opens from the bar's Focus button: start a timed session, or see and end the current one.</summary>
public partial class FocusFlyout : FlyoutWindow
{
    private readonly FocusController _focus;
    private readonly DeadlineService _deadlines;
    private readonly Action _openSettings;

    internal FocusFlyout(FocusController focus, DeadlineService deadlines, Action openSettings)
    {
        _focus = focus;
        _deadlines = deadlines;
        _openSettings = openSettings;
        InitializeComponent();

        focus.Changed += OnChanged;
        Closed += (_, _) => focus.Changed -= OnChanged;
        Refresh();
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var settings = _focus.Settings;
        var session = _focus.Active;
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;

        OffPanel.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;
        OnPanel.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
        HeadingText.Text = session is null ? "Focus Mode" : "Focus Mode is on";

        if (session is not null)
        {
            ReasonText.Text = session.Deadline is { } deadline
                ? $"For \"{deadline.Title}\""
                : "Focus session";
            EndsText.Text = $"Ends at {session.EndsAt.ToLocalTime().ToString("t", culture)} · {FocusPlanner.Remaining(session, now)} left · {settings.Strictness}";

            (WebsiteText.Text, var retry) = _focus.Websites switch
            {
                WebsiteStatus.Blocked => ($"{Count(settings.BlockedSites.Count, "website")} blocked.", false),
                WebsiteStatus.Working => ("Blocking websites… (check for a Windows permission prompt)", false),
                WebsiteStatus.Declined => ("Websites aren't blocked because the permission prompt was declined.", true),
                WebsiteStatus.Failed => ("Websites couldn't be blocked. Details are in the log.", true),
                _ when settings.Strictness == Strictness.Gentle && settings.BlockedSites.Count > 0 => ("Gentle mode doesn't block websites.", false),
                _ => ("", false),
            };
            WebsiteText.Visibility = WebsiteText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            RetryButton.Visibility = retry ? Visibility.Visible : Visibility.Collapsed;

            CompleteButton.Visibility = session.Deadline is null ? Visibility.Collapsed : Visibility.Visible;
            CompleteButton.Content = "I finished it: mark complete";

            EndButton.Visibility = _focus.CanEnd ? Visibility.Visible : Visibility.Collapsed;
            StrictText.Text = _focus.CanEnd ? "" : session.Reason == FocusReason.Deadline
                ? "Strict mode: Focus Mode stays on until the deadline passes or you mark it complete."
                : "Strict mode: Focus Mode stays on until the session ends.";
            StrictText.Visibility = StrictText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        SummaryText.Text = settings.BlockedApps.Count + settings.BlockedSites.Count == 0
            ? "Nothing is on your block lists yet."
            : $"Blocking {Count(settings.BlockedApps.Count, "app")} and {Count(settings.BlockedSites.Count, "website")}.";
        AutoText.Text = settings.AutoEnable
            ? $"Turns on by itself {ReminderSettings.Describe(settings.StartBeforeDeadline)} before a deadline."
            : "Doesn't turn on by itself.";
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    private void StartManual_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string minutes }) _focus.StartManual(TimeSpan.FromMinutes(int.Parse(minutes, CultureInfo.InvariantCulture)));
    }

    private void End_Click(object sender, RoutedEventArgs e) => _focus.End();

    private void Retry_Click(object sender, RoutedEventArgs e) => _focus.RetryWebsites();

    private void Complete_Click(object sender, RoutedEventArgs e)
    {
        if (_focus.Active?.Deadline is { } deadline) _deadlines.SetComplete(deadline.Id, true);
        CloseFlyout();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        _openSettings();
    }
}
