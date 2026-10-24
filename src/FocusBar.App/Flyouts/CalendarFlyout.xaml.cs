using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;

namespace FocusBar.App.Flyouts;

/// <summary>Opens from the clock: today's date and a month calendar. Up/Down arrows or the buttons change month.</summary>
public partial class CalendarFlyout : FlyoutWindow
{
    private readonly ErrorLog _log;

    internal CalendarFlyout(ErrorLog log)
    {
        _log = log;
        InitializeComponent();

        var now = DateTime.Now;
        TodayText.Text = now.ToString("D", CultureInfo.CurrentCulture);
        TimeText.Text = now.ToString("t", CultureInfo.CurrentCulture);
        PreviewKeyDown += OnArrowKeys;
    }

    private void OnArrowKeys(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.PageUp) { Calendar.ShowPreviousMonth(); e.Handled = true; }
        else if (e.Key is Key.Down or Key.PageDown) { Calendar.ShowNextMonth(); e.Handled = true; }
    }

    private void DateTimeSettings_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        AppLauncher.OpenSettings("ms-settings:dateandtime", _log);
    }
}
