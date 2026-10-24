using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using FocusBar.Core.Status;

namespace FocusBar.App.Controls;

/// <summary>
/// A month calendar drawn with the bar's own colors (WPF's built-in calendar can't follow dark mode).
/// Read-only for the clock flyout; clickable for picking a due date when <see cref="IsSelectable"/> is set.
/// </summary>
public partial class MonthCalendar : UserControl
{
    private DateOnly _month;
    private DateOnly? _selected;
    private bool _isSelectable;

    public MonthCalendar()
    {
        InitializeComponent();

        var culture = CultureInfo.CurrentCulture;
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

        var today = Today;
        _month = new DateOnly(today.Year, today.Month, 1);
        Render();
    }

    /// <summary>Raised when the user clicks a day (only when <see cref="IsSelectable"/>).</summary>
    public event EventHandler<DateOnly>? DateSelected;

    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            _isSelectable = value;
            Render();
        }
    }

    public DateOnly? SelectedDate
    {
        get => _selected;
        set
        {
            _selected = value;
            if (value is { } date) _month = new DateOnly(date.Year, date.Month, 1);
            Render();
        }
    }

    public void ShowMonth(DateOnly anyDayInMonth)
    {
        _month = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        Render();
    }

    public void ShowPreviousMonth() => ShowMonth(_month.AddMonths(-1));

    public void ShowNextMonth() => ShowMonth(_month.AddMonths(1));

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private void Render()
    {
        var culture = CultureInfo.CurrentCulture;
        MonthText.Text = _month.ToDateTime(TimeOnly.MinValue).ToString("Y", culture);

        var today = Today;
        DayGrid.Children.Clear();
        foreach (var date in MonthGrid.Build(_month.Year, _month.Month, culture.DateTimeFormat.FirstDayOfWeek))
        {
            DayGrid.Children.Add(IsSelectable ? DayButton(date, today, culture) : DayLabel(date, today, culture));
        }
    }

    private FrameworkElement DayLabel(DateOnly date, DateOnly today, CultureInfo culture)
    {
        var label = new TextBlock
        {
            Text = date.Day.ToString(culture),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var cell = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = label,
        };

        if (date == today)
        {
            label.FontWeight = FontWeights.SemiBold;
            cell.SetResourceReference(Border.BackgroundProperty, "Accent");
            label.SetResourceReference(TextBlock.ForegroundProperty, "AccentForeground");
        }
        else
        {
            label.SetResourceReference(TextBlock.ForegroundProperty, date.Month == _month.Month ? "FlyoutForeground" : "FlyoutMutedForeground");
        }
        return cell;
    }

    private FrameworkElement DayButton(DateOnly date, DateOnly today, CultureInfo culture)
    {
        var button = new Button
        {
            Content = date.Day.ToString(culture),
            Style = (Style)FindResource("DayButton"),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = date.ToDateTime(TimeOnly.MinValue).ToString("D", culture),
        };
        AutomationProperties.SetName(button, date.ToDateTime(TimeOnly.MinValue).ToString("D", culture));

        if (date == _selected)
        {
            button.FontWeight = FontWeights.SemiBold;
            button.SetResourceReference(BackgroundProperty, "Accent");
            button.SetResourceReference(ForegroundProperty, "AccentForeground");
        }
        else
        {
            if (date == today) button.SetResourceReference(BorderBrushProperty, "Accent");
            if (date.Month != _month.Month) button.SetResourceReference(ForegroundProperty, "FlyoutMutedForeground");
        }

        // Redrawing replaces this button, so wait until its click has finished.
        button.Click += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            SelectedDate = date;
            DateSelected?.Invoke(this, date);
        });
        return button;
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ShowPreviousMonth();

    private void NextMonth_Click(object sender, RoutedEventArgs e) => ShowNextMonth();

    private void ThisMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(Today);
}
