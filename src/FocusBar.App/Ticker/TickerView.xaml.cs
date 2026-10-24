using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using FocusBar.App.Deadlines;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.App.Ticker;

/// <summary>
/// The scrolling deadline ticker. Shows every incomplete deadline with a live countdown, colored by urgency.
/// It only scrolls when the deadlines don't fit, pauses on hover and behind full-screen apps, and stays
/// still if the user turned off animations in Windows.
/// </summary>
public partial class TickerView : UserControl
{
    /// <summary>Scroll speed in device-independent pixels per second: slow enough to read.</summary>
    private const double Speed = 40;

    /// <summary>30 frames a second is smooth at this speed and costs half the CPU of the default 60.</summary>
    private const int FrameRate = 30;

    private const double ItemGap = 28;

    private readonly DispatcherTimer _minuteTimer = new(DispatcherPriority.Background);
    private readonly HashSet<string> _pauseReasons = [];

    private DeadlineService? _deadlines;
    private Action? _openList;
    private FlyoutToggle<DeadlineFormFlyout>? _form;
    private DeadlineDetailsFlyout? _details;
    private (Guid Id, DateTime At) _lastDetailsClosed;
    private AnimationClock? _scroll;

    public TickerView()
    {
        InitializeComponent();
        _minuteTimer.Tick += (_, _) => Refresh();
    }

    private bool IsPaused => _pauseReasons.Count > 0;

    internal void Initialize(DeadlineService deadlines, Action openList)
    {
        _deadlines = deadlines;
        _openList = openList;
        _form = new FlyoutToggle<DeadlineFormFlyout>(() => new DeadlineFormFlyout(deadlines));

        deadlines.Changed += OnDeadlinesChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        Refresh();
    }

    /// <summary>Stops timers and the animation when the bar closes.</summary>
    internal void Shutdown()
    {
        _minuteTimer.Stop();
        StopScrolling();
        _form?.Close();
        _details?.CloseFlyout();
        if (_deadlines is not null) _deadlines.Changed -= OnDeadlinesChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
    }

    /// <summary>Pause for a reason (hover, open details, full-screen app); resumes when every reason is cleared.</summary>
    internal void SetPaused(string reason, bool paused)
    {
        var changed = paused ? _pauseReasons.Add(reason) : _pauseReasons.Remove(reason);
        if (!changed || _scroll?.Controller is not { } controller) return;

        if (IsPaused) controller.Pause(); else controller.Resume();
    }

    private void OnDeadlinesChanged(object? sender, EventArgs e) => Refresh();

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) Dispatcher.BeginInvoke(Refresh);
    }

    /// <summary>
    /// Rebuilds the strip from the current deadlines and countdowns. Runs on every change and at the start
    /// of each minute; the scroll position carries over so the ticker doesn't jump.
    /// </summary>
    private void Refresh()
    {
        if (_deadlines is null) return;

        var now = DateTimeOffset.Now;
        ScheduleNextMinute(now);
        UpdateClassChip(now);

        var upcoming = _deadlines.Upcoming();
        Strip.Children.Clear();

        if (upcoming.Count == 0)
        {
            StopScrolling();
            Track.Visibility = Visibility.Collapsed;
            EmptyButton.Visibility = Visibility.Visible;
            Viewport.OpacityMask = null;
            AutomationProperties.SetName(Viewport, "No upcoming deadlines");
            return;
        }

        Track.Visibility = Visibility.Visible;
        EmptyButton.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(Viewport,
            $"{upcoming.Count} upcoming deadline{(upcoming.Count == 1 ? "" : "s")}. Next: {DeadlineDisplay.Summary(upcoming[0], now)}");

        AddCopy(upcoming, now);
        Strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var copyWidth = Strip.DesiredSize.Width;
        var available = Viewport.ActualWidth;
        var overflows = copyWidth - ItemGap > available;

        if (overflows && SystemParameters.ClientAreaAnimation && available > 0)
        {
            // Three copies side by side: wherever the loop is, the visible part is always covered.
            AddCopy(upcoming, now);
            AddCopy(upcoming, now);
            StartScrolling(copyWidth);
            Viewport.OpacityMask = FadeEdges(left: true);
        }
        else
        {
            StopScrolling();
            Viewport.OpacityMask = overflows ? FadeEdges(left: false) : null;
        }
    }

    private void ScheduleNextMinute(DateTimeOffset now)
    {
        _minuteTimer.Stop();
        _minuteTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _minuteTimer.Start();
    }

    private void UpdateClassChip(DateTimeOffset now)
    {
        var current = ScheduleLookup.CurrentOrNext(_deadlines!.Schedule, now.LocalDateTime);
        if (current is null)
        {
            ClassChip.Visibility = Visibility.Collapsed;
            return;
        }

        ClassText.Text = ScheduleLookup.Describe(current, DateOnly.FromDateTime(now.LocalDateTime), CultureInfo.CurrentCulture);
        ClassChip.ToolTip = ClassText.Text;
        ClassChip.Visibility = Visibility.Visible;
    }

    private void AddCopy(IReadOnlyList<Deadline> deadlines, DateTimeOffset now)
    {
        foreach (var deadline in deadlines) Strip.Children.Add(CreateItem(deadline, now));
    }

    /// <summary>One ticker entry: ● Bio lab · Biology  2d 4h</summary>
    private FrameworkElement CreateItem(Deadline deadline, DateTimeOffset now)
    {
        var urgency = UrgencyRules.Classify(deadline, now);
        var brushKey = DeadlineDisplay.BrushKey(urgency);

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, brushKey);
        row.Children.Add(dot);

        row.Children.Add(new TextBlock { Text = deadline.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

        if (!string.IsNullOrWhiteSpace(deadline.ClassName))
        {
            var className = new TextBlock { Text = " · " + deadline.ClassName, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            className.SetResourceReference(TextBlock.ForegroundProperty, "BarMutedForeground");
            row.Children.Add(className);
        }

        var countdown = new TextBlock
        {
            Text = Countdown.Format(deadline.DueAt, now),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (urgency == Urgency.Overdue)
        {
            // Dark red is hard to read as text on a dark bar, so overdue gets a filled pill instead.
            countdown.SetResourceReference(TextBlock.ForegroundProperty, "UrgencyOverdueForeground");
            var pill = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(10, 0, 0, 0), Child = countdown, VerticalAlignment = VerticalAlignment.Center };
            pill.SetResourceReference(Border.BackgroundProperty, brushKey);
            row.Children.Add(pill);
        }
        else
        {
            countdown.Margin = new Thickness(10, 0, 0, 0);
            countdown.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            row.Children.Add(countdown);
        }

        var item = new Border
        {
            Child = row,
            Background = Brushes.Transparent,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, ItemGap - 12, 0),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            ToolTip = DeadlineDisplay.Summary(deadline, now),
            Tag = deadline.Id,
        };
        item.MouseEnter += (_, _) => item.SetResourceReference(Border.BackgroundProperty, "BarHover");
        item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
        item.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenDetails(item, deadline.Id);
        };
        return item;
    }

    private void StartScrolling(double period)
    {
        // Carry the current position over to the new loop so a refresh doesn't make the ticker jump.
        var x = StopScrolling() % period;
        if (x > 0) x -= period;

        var animation = new DoubleAnimation(x, x - period, TimeSpan.FromSeconds(period / Speed))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Timeline.SetDesiredFrameRate(animation, FrameRate);

        _scroll = animation.CreateClock();
        Shift.ApplyAnimationClock(TranslateTransform.XProperty, _scroll);
        if (IsPaused) _scroll.Controller?.Pause();
    }

    /// <summary>Stops the animation and puts the strip back at the start. Returns where it was.</summary>
    private double StopScrolling()
    {
        var x = Shift.X;
        if (_scroll is not null)
        {
            Shift.ApplyAnimationClock(TranslateTransform.XProperty, null);
            _scroll = null;
        }
        Shift.X = 0;
        return x;
    }

    private static Brush FadeEdges(bool left)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        brush.GradientStops.Add(new GradientStop(left ? Colors.Transparent : Colors.Black, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Black, left ? 0.04 : 0));
        brush.GradientStops.Add(new GradientStop(Colors.Black, 0.94));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        brush.Freeze();
        return brush;
    }

    private void OpenDetails(FrameworkElement anchor, Guid id)
    {
        if (_deadlines?.Find(id) is not { } deadline) return;

        // Clicking the same entry again closes its details (the click already closed them by taking focus).
        if (_lastDetailsClosed.Id == id && DateTime.UtcNow - _lastDetailsClosed.At < TimeSpan.FromMilliseconds(300)) return;

        _details?.CloseFlyout();
        var details = new DeadlineDetailsFlyout(deadline, _deadlines, onEdit: OpenEditor, onOpenList: () => _openList?.Invoke());
        details.FlyoutClosed += (_, _) =>
        {
            _lastDetailsClosed = (id, DateTime.UtcNow);
            if (_details == details) _details = null;
            SetPaused("details", false);
        };
        _details = details;
        SetPaused("details", true);
        details.ShowAbove(anchor, HorizontalAlignment.Left);
    }

    private void OpenEditor(Deadline deadline)
    {
        if (_deadlines is null) return;
        new DeadlineFormFlyout(_deadlines, deadline).ShowAbove(AddButton, HorizontalAlignment.Right);
    }

    private void Viewport_MouseEnter(object sender, MouseEventArgs e) => SetPaused("hover", true);

    private void Viewport_MouseLeave(object sender, MouseEventArgs e) => SetPaused("hover", false);

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) Refresh();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => _form?.Toggle(AddButton, HorizontalAlignment.Right);

    private void ListButton_Click(object sender, RoutedEventArgs e) => _openList?.Invoke();
}
