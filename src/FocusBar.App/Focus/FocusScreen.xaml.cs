using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Focus;
using FocusBar.Core.Models;

namespace FocusBar.App.Focus;

/// <summary>What the student can do on the Focus screen, per strictness level.</summary>
internal interface IFocusScreenActions
{
    /// <summary>Gentle: let this app run for the rest of the session.</summary>
    void AllowForSession(string appName);

    /// <summary>Gentle: close the app after all ("Back to work").</summary>
    void CloseApp(string appName);
}

/// <summary>
/// The full-screen message shown when a blocked app is opened during Focus Mode:
/// <list type="bullet">
/// <item>Gentle: "Discord is on your block list" with Back to work (closes it) / Continue anyway.</item>
/// <item>Soft: the app was closed; an Override button unlocks after a short wait.</item>
/// <item>Strict: the app was closed; no override until the session ends.</item>
/// </list>
/// "Back to work" (or Esc) always closes this screen; it never traps the student.
/// </summary>
public partial class FocusScreen : Window
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private IFocusScreenActions? _actions;
    private FocusSession? _session;
    private string _appName = "";
    private DateTimeOffset _overrideAt;
    private bool _overridable;
    private bool _gentle;

    public FocusScreen()
    {
        InitializeComponent();

        // Cover the whole primary screen, including the bar.
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _timer.Tick += (_, _) => UpdateCountdown();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Shows (or updates) the screen for a blocked app.</summary>
    internal void ShowFor(BlockedProcess app, FocusSession session, Strictness strictness, TimeSpan overrideDelay,
        IFocusScreenActions actions, string? imagePath)
    {
        _actions = actions;
        _session = session;
        _appName = app.Name;
        SetImage(imagePath);

        ContinueButton.Visibility = Visibility.Collapsed;
        OverrideButton.Visibility = Visibility.Collapsed;
        _overridable = false;
        _gentle = strictness == Strictness.Gentle;

        switch (strictness)
        {
            case Strictness.Gentle:
                HeadingText.Text = "Are you sure?";
                MessageText.Text = $"{app.DisplayName} is on your Focus Mode block list.";
                ContinueButton.Visibility = Visibility.Visible;
                UnlockText.Text = $"Back to work closes {app.DisplayName}. Continue anyway keeps it open for the rest of this session.";
                break;

            case Strictness.Soft:
                HeadingText.Text = "Time to focus";
                MessageText.Text = $"{app.DisplayName} is blocked during Focus Mode, so FocusBar closed it.";
                _overrideAt = DateTimeOffset.Now + overrideDelay;
                _overridable = true;
                OverrideButton.Visibility = Visibility.Visible;
                break;

            default:
                HeadingText.Text = "Time to focus";
                MessageText.Text = $"{app.DisplayName} is blocked until Focus Mode ends, so FocusBar closed it.";
                UnlockText.Text = session.Reason == FocusReason.Deadline
                    ? "Strict mode: it unlocks when the deadline passes or you mark it complete."
                    : "Strict mode: it unlocks when your focus session ends.";
                break;
        }

        UpdateCountdown();
        _timer.Start();

        if (!IsVisible) Show();
        Activate();
        BackButton.Focus();
    }

    private void SetImage(string? path)
    {
        BitmapImage? image = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // don't keep the file locked
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
            {
                image = null;
            }
        }

        BackgroundImage.Source = image;
        BackgroundImage.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        Scrim.Visibility = BackgroundImage.Visibility;
    }

    private void UpdateCountdown()
    {
        if (_session is null) return;
        var now = DateTimeOffset.Now;

        ReasonText.Text = _session.Deadline is { } deadline
            ? $"{deadline.Title} is due at {deadline.DueAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}"
            : $"Your focus session ends at {_session.EndsAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}";
        CountdownText.Text = Countdown.Format(_session.EndsAt, now) + " left";

        if (!_overridable) return;
        var wait = _overrideAt - now;
        if (wait > TimeSpan.Zero)
        {
            OverrideButton.IsEnabled = false;
            OverrideButton.Content = $"Override in {Math.Ceiling(wait.TotalSeconds):0} s";
            UnlockText.Text = "Take a breath. If you really need it, you can override after the wait.";
        }
        else
        {
            OverrideButton.IsEnabled = true;
            OverrideButton.Content = "Override";
            UnlockText.Text = $"Override lets {_appName} run for the rest of this session.";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        // Soft and Strict already closed the app; Gentle closes it only now that the student chose to.
        if (_gentle) _actions?.CloseApp(_appName);
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        _actions?.AllowForSession(_appName);
        Close();
    }

    private void Override_Click(object sender, RoutedEventArgs e)
    {
        if (DateTimeOffset.Now < _overrideAt) return;
        _actions?.AllowForSession(_appName);
        Close();
    }
}
