using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.Core.Focus;

namespace FocusBar.App.Focus;

/// <summary>The Focus Mode button on the bar.</summary>
public partial class FocusButton : UserControl
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
    private FocusController? _focus;
    private FlyoutToggle<FocusFlyout>? _flyout;

    public FocusButton()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Refresh();
    }

    internal void Initialize(FocusController focus, DeadlineService deadlines, Action openSettings)
    {
        _focus = focus;
        _flyout = new FlyoutToggle<FocusFlyout>(() => new FocusFlyout(focus, deadlines, openSettings));
        focus.Changed += OnChanged;
        Refresh();
    }

    internal void Shutdown()
    {
        _timer.Stop();
        _flyout?.Close();
        if (_focus is not null) _focus.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_focus?.Active is { } session)
        {
            var left = FocusPlanner.Remaining(session, DateTimeOffset.Now);
            TimeText.Text = left;
            TimeText.Visibility = Visibility.Visible;
            Button.SetResourceReference(BackgroundProperty, "Accent");
            Button.SetResourceReference(ForegroundProperty, "AccentForeground");
            Button.ToolTip = $"Focus Mode is on: {left} left";
            AutomationProperties.SetName(Button, $"Focus Mode is on, {left} left");
            _timer.Start();
        }
        else
        {
            TimeText.Visibility = Visibility.Collapsed;
            Button.Background = Brushes.Transparent;
            Button.SetResourceReference(ForegroundProperty, "BarForeground");
            Button.ToolTip = "Focus Mode";
            AutomationProperties.SetName(Button, "Focus Mode");
            _timer.Stop();
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e) => _flyout?.Toggle(this, HorizontalAlignment.Right);
}
