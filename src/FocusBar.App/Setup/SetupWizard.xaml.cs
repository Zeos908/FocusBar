using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using FocusBar.App.Deadlines;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Settings;
using FocusBar.App.Shell;
using FocusBar.App.Themes;

namespace FocusBar.App.Setup;

/// <summary>
/// The first-run guide: welcome → deadlines → reminders → Focus Mode → appearance → done. The middle steps reuse the
/// Settings pages, so every choice is saved as it's made and Back/Skip never lose anything. Opens by itself once;
/// Settings → General can run it again.
/// </summary>
public partial class SetupWizard : Window
{
    private const int StepCount = 6;
    private static SetupWizard? _open;

    private readonly BarServices _services;
    private int _step;
    private bool _loading;

    private SetupWizard(BarServices services)
    {
        _services = services;
        InitializeComponent();

        for (var i = 0; i < StepCount; i++) Dots.Items.Add(new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 6, 0) });

        services.Deadlines.Changed += OnDeadlinesChanged;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Closed += (_, _) =>
        {
            _open = null;
            services.Deadlines.Changed -= OnDeadlinesChanged;
            // Finished, skipped, or closed: either way, don't open by itself again.
            if (!services.Settings.Current.SetupCompleted) services.Settings.Update(s => s with { SetupCompleted = true });
        };
        ShowStep(0);
    }

    /// <summary>Opens the guide, or brings it to the front if it's already open.</summary>
    internal static void ShowSingle(BarServices services)
    {
        _open ??= new SetupWizard(services);
        _open.Show();
        if (_open.WindowState == WindowState.Minimized) _open.WindowState = WindowState.Normal;
        _open.Activate();
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, StepCount - 1);

        WelcomeStep.Visibility = Visibility.Collapsed;
        DeadlinesStep.Visibility = Visibility.Collapsed;
        PageHost.Visibility = Visibility.Collapsed;
        DoneStep.Visibility = Visibility.Collapsed;
        SourcesHost.Content = null;
        PageHost.Content = null; // pages unsubscribe from services when they're unloaded

        switch (_step)
        {
            case 0:
                WelcomeStep.Visibility = Visibility.Visible;
                break;
            case 1:
                SourcesHost.Content = new SourcesPage(_services.Sync, _services.Log);
                UpdateDeadlineCount();
                DeadlinesStep.Visibility = Visibility.Visible;
                break;
            case 2:
                ShowPage(new RemindersPage(_services.Settings, _services.Reminders));
                break;
            case 3:
                ShowPage(new FocusPage(_services.Settings, _services.Apps, _services.Focus));
                break;
            case 4:
                ShowPage(new AppearancePage(_services.Appearance, _services.Log));
                break;
            default:
                _loading = true;
                StartupBox.IsChecked = StartWithWindows.IsOn();
                _loading = false;
                DoneStep.Visibility = Visibility.Visible;
                break;
        }

        StepText.Text = $"Step {_step + 1} of {StepCount}";
        for (var i = 0; i < StepCount; i++)
        {
            ((Ellipse)Dots.Items[i]).SetResourceReference(Shape.FillProperty, i <= _step ? "Accent" : "InputBorder");
        }
        BackButton.Visibility = _step == 0 ? Visibility.Hidden : Visibility.Visible;
        SkipButton.Visibility = _step == StepCount - 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = _step == 0 ? "Get started" : _step == StepCount - 1 ? "Finish" : "Next";
        Scroller.ScrollToTop();
        NextButton.Focus();
    }

    private void ShowPage(FrameworkElement page)
    {
        PageHost.Content = page;
        PageHost.Visibility = Visibility.Visible;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == StepCount - 1) Close();
        else ShowStep(_step + 1);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

    private void Skip_Click(object sender, RoutedEventArgs e) => Close();

    private void AddDeadline_Click(object sender, RoutedEventArgs e) => new DeadlineDialog(_services.Deadlines, null, this).ShowDialog();

    private void OnDeadlinesChanged(object? sender, EventArgs e) => UpdateDeadlineCount();

    private void UpdateDeadlineCount()
    {
        var count = _services.Deadlines.ForReminders().Count(d => !d.IsComplete);
        DeadlineCount.Text = count switch
        {
            0 => "No deadlines yet.",
            1 => "1 deadline so far.",
            _ => $"{count} deadlines so far.",
        };
    }

    private void StartupBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var on = StartupBox.IsChecked == true;
        if (!StartWithWindows.Set(on, _services.Log))
        {
            _loading = true;
            StartupBox.IsChecked = !on;
            _loading = false;
        }
    }
}
