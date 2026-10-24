using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FocusBar.App.Themes;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
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
    private readonly ErrorLog _log;
    private string? _imagePath;
    private IFocusScreenActions? _actions;
    private FocusSession? _session;
    private string _appName = "";
    private DateTimeOffset _overrideAt;
    private bool _overridable;
    private bool _gentle;

    internal FocusScreen(ErrorLog log)
    {
        _log = log;
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

    /// <summary>
    /// The theme's Focus screen image, or the built-in gradient. Loaded off the UI thread at the screen's real
    /// pixel size, so the screen appears at once and the picture fades in sharp.
    /// </summary>
    private async void SetImage(string? path)
    {
        if (path == _imagePath) return;
        _imagePath = path;

        if (path is null)
        {
            ShowImage(null);
            return;
        }

        var (width, height) = (Width, Height);
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var log = _log;
        LoadedImage? image = null;
        try
        {
            image = await Task.Run(() => ThemeImageLoader.Load(path, width, height, dpi, ImageFit.Fill, log));
        }
        catch (Exception ex)
        {
            log.Write(ex, "Could not load the Focus screen image.");
        }
        if (path == _imagePath) ShowImage(image);
    }

    private void ShowImage(LoadedImage? image)
    {
        BackgroundImage.Source = image?.Bitmap;
        if (image is not null)
        {
            BackgroundImage.Width = image.Width;
            BackgroundImage.Height = image.Height;
        }
        BackgroundImage.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        Scrim.Visibility = BackgroundImage.Visibility;
    }

    /// <summary>
    /// Settings → Appearance → Preview: what the Focus screen looks like with this image, using made-up details.
    /// Only "Back to work" (or Esc) is shown.
    /// </summary>
    internal void ShowPreview(string? imagePath)
    {
        _actions = null;
        _overridable = false;
        _gentle = false;
        _session = new FocusSession(FocusReason.Manual, DateTimeOffset.Now, DateTimeOffset.Now.AddMinutes(25), null);
        SetImage(imagePath);

        HeadingText.Text = "Time to focus";
        MessageText.Text = "This is a preview. When a blocked app opens during Focus Mode, this screen appears.";
        UnlockText.Text = "Press Esc or Back to work to close it.";
        ContinueButton.Visibility = Visibility.Collapsed;
        OverrideButton.Visibility = Visibility.Collapsed;

        UpdateCountdown();
        _timer.Start();
        Show();
        Activate();
        BackButton.Focus();
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
