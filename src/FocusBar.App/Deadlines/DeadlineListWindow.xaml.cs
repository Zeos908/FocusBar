using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.App.Ticker;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.App.Deadlines;

/// <summary>
/// Every deadline in one place: upcoming and completed, with mark complete, edit, and delete.
/// Only one copy is open at a time; the bar's list button brings it to the front.
/// </summary>
public partial class DeadlineListWindow : Window
{
    private static DeadlineListWindow? _open;

    private readonly DeadlineService _deadlines;
    private readonly DispatcherTimer _minuteTimer = new(DispatcherPriority.Background);

    private DeadlineListWindow(DeadlineService deadlines)
    {
        _deadlines = deadlines;
        InitializeComponent();

        _deadlines.Changed += OnDeadlinesChanged;
        _minuteTimer.Tick += (_, _) => Refresh();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Closed += (_, _) =>
        {
            _minuteTimer.Stop();
            _deadlines.Changed -= OnDeadlinesChanged;
            _open = null;
        };

        Refresh();
    }

    /// <summary>Opens the list, or brings the already-open one to the front.</summary>
    internal static void ShowSingle(DeadlineService deadlines)
    {
        if (_open is null)
        {
            _open = new DeadlineListWindow(deadlines);
            _open.Show();
        }

        if (_open.WindowState == WindowState.Minimized) _open.WindowState = WindowState.Normal;
        _open.Activate();
    }

    private bool ShowingCompleted => CompletedTab.IsChecked == true;

    private void OnDeadlinesChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var now = DateTimeOffset.Now;
        _minuteTimer.Stop();
        _minuteTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _minuteTimer.Start();

        var upcoming = _deadlines.Upcoming();
        var completed = _deadlines.Completed();
        UpcomingTab.Content = $"Upcoming ({upcoming.Count})";
        CompletedTab.Content = $"Completed ({completed.Count})";

        var shown = ShowingCompleted ? completed : upcoming;
        Rows.ItemsSource = shown.Select(d => new DeadlineRow(d, now)).ToList();

        EmptyText.Text = ShowingCompleted
            ? "Nothing completed yet."
            : "Nothing due. Add a deadline with the button above (or Ctrl+N).";
        EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (IsInitialized && _deadlines is not null) Refresh();
    }

    private static DeadlineRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as DeadlineRow;

    private void Add_Click(object sender, RoutedEventArgs e) => new DeadlineDialog(_deadlines, null, this).ShowDialog();

    private void New_Executed(object sender, ExecutedRoutedEventArgs e) => Add_Click(sender, e);

    private void ToggleComplete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) _deadlines.SetComplete(row.Id, !row.IsComplete);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row && _deadlines.Find(row.Id) is { } deadline)
        {
            new DeadlineDialog(_deadlines, deadline, this).ShowDialog();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;

        var confirm = MessageBox.Show(this, $"Delete \"{row.Title}\"?", "FocusBar",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (confirm == MessageBoxResult.Yes) _deadlines.Delete(row.Id);
    }
}

/// <summary>One row in the deadline list.</summary>
internal sealed class DeadlineRow(Deadline deadline, DateTimeOffset now)
{
    public Guid Id => deadline.Id;
    public string Title => deadline.Title;
    public bool IsComplete => deadline.IsComplete;
    public Urgency Urgency { get; } = UrgencyRules.Classify(deadline, now);

    public string Meta => string.IsNullOrWhiteSpace(deadline.ClassName)
        ? DeadlineDisplay.ShortDue(deadline)
        : $"{deadline.ClassName} · {DeadlineDisplay.ShortDue(deadline)}";

    public string Countdown => deadline.IsComplete ? "Done" : Core.Deadlines.Countdown.Format(deadline.DueAt, now);

    public string ToolTip => string.IsNullOrWhiteSpace(deadline.Description)
        ? DeadlineDisplay.SourceText(deadline.Source)
        : $"{deadline.Description}\n\n{DeadlineDisplay.SourceText(deadline.Source)}";

    public string CompleteLabel => deadline.IsComplete ? $"Mark {Title} not complete" : $"Mark {Title} complete";
    public string EditLabel => $"Edit {Title}";
    public string DeleteLabel => $"Delete {Title}";
}
