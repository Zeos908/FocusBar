using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.App.Ticker;

/// <summary>Opens when a ticker entry is clicked: the full details, plus Mark complete and Edit.</summary>
public partial class DeadlineDetailsFlyout : FlyoutWindow
{
    private readonly Deadline _deadline;
    private readonly DeadlineService _deadlines;
    private readonly Action<Deadline> _onEdit;
    private readonly Action _onOpenList;

    internal DeadlineDetailsFlyout(Deadline deadline, DeadlineService deadlines, Action<Deadline> onEdit, Action onOpenList)
    {
        _deadline = deadline;
        _deadlines = deadlines;
        _onEdit = onEdit;
        _onOpenList = onOpenList;
        InitializeComponent();

        var now = DateTimeOffset.Now;
        var urgency = UrgencyRules.Classify(deadline, now);
        var countdown = Countdown.Format(deadline.DueAt, now);

        CountdownText.Text = urgency == Urgency.Overdue ? countdown : $"Due in {countdown}";
        UrgencyPill.SetResourceReference(Border.BackgroundProperty, DeadlineDisplay.BrushKey(urgency));
        // The dark-mode urgency colors are bright, so black text reads best on them; the light-mode ones
        // (and overdue in both) are dark and get white text.
        CountdownText.Foreground = ThemeManager.IsLight || urgency == Urgency.Overdue
            ? System.Windows.Media.Brushes.White
            : System.Windows.Media.Brushes.Black;

        TitleText.Text = deadline.Title;
        ClassText.Text = deadline.ClassName ?? "";
        ClassRow.Visibility = string.IsNullOrWhiteSpace(deadline.ClassName) ? Visibility.Collapsed : Visibility.Visible;
        DueText.Text = DeadlineDisplay.LongDue(deadline);
        DescriptionText.Text = deadline.Description ?? "";
        DescriptionArea.Visibility = string.IsNullOrWhiteSpace(deadline.Description) ? Visibility.Collapsed : Visibility.Visible;
        SourceText.Text = DeadlineDisplay.SourceText(deadline.Source);
        // Synced deadlines are edited at their source; changes here would be overwritten by the next sync.
        EditButton.Visibility = deadline.Source == DeadlineSource.Manual ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => CompleteButton.Focus();
    }

    private void Complete_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        _deadlines.SetComplete(_deadline.Id, true);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        // Open the editor once this flyout has gone, so its closing doesn't steal focus from the editor.
        Dispatcher.BeginInvoke(() => _onEdit(_deadline));
    }

    private void AllDeadlines_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        _onOpenList();
    }
}
