using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Status;

namespace FocusBar.App.Flyouts;

/// <summary>Opens from the clock: today's date and a month calendar. Up/Down arrows or the buttons change month.</summary>
public partial class CalendarFlyout : FlyoutWindow
{
    private readonly ErrorLog _log;
    private DateOnly _shownMonth;

    internal CalendarFlyout(ErrorLog log)
    {
        _log = log;
        InitializeComponent();

        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        TodayText.Text = now.ToString("D", culture);
        TimeText.Text = now.ToString("t", culture);

        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)firstDay + i) % 7);
            WeekdayHeader.Children.Add(new TextBlock
            {
                Text = culture.DateTimeFormat.GetShortestDayName(day),
                Style = (Style)FindResource("FlyoutMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        ShowMonth(new DateOnly(now.Year, now.Month, 1));
        PreviewKeyDown += OnArrowKeys;
    }

    private void ShowMonth(DateOnly month)
    {
        _shownMonth = month;
        var culture = CultureInfo.CurrentCulture;
        MonthText.Text = month.ToDateTime(TimeOnly.MinValue).ToString("Y", culture);

        var today = DateOnly.FromDateTime(DateTime.Now);
        DayGrid.Children.Clear();
        foreach (var date in MonthGrid.Build(month.Year, month.Month, culture.DateTimeFormat.FirstDayOfWeek))
        {
            var isToday = date == today;
            var cell = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = date.Day.ToString(culture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
                },
            };

            var label = (TextBlock)cell.Child;
            if (isToday)
            {
                cell.SetResourceReference(Border.BackgroundProperty, "Accent");
                label.SetResourceReference(TextBlock.ForegroundProperty, "AccentForeground");
            }
            else
            {
                label.SetResourceReference(TextBlock.ForegroundProperty,
                    date.Month == month.Month ? "FlyoutForeground" : "FlyoutMutedForeground");
            }

            DayGrid.Children.Add(cell);
        }
    }

    private void OnArrowKeys(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.PageUp) { ShowMonth(_shownMonth.AddMonths(-1)); e.Handled = true; }
        else if (e.Key is Key.Down or Key.PageDown) { ShowMonth(_shownMonth.AddMonths(1)); e.Handled = true; }
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(_shownMonth.AddMonths(-1));

    private void NextMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(_shownMonth.AddMonths(1));

    private void ThisMonth_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        ShowMonth(new DateOnly(now.Year, now.Month, 1));
    }

    private void DateTimeSettings_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        AppLauncher.OpenSettings("ms-settings:dateandtime", _log);
    }
}
