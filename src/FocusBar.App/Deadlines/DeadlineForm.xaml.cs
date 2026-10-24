using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusBar.App.Services;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.App.Deadlines;

/// <summary>
/// The form for adding a deadline or editing one. Hosted in a flyout above the bar (quick add) or in a
/// dialog (editing from the deadline list).
/// </summary>
public partial class DeadlineForm : UserControl
{
    private const int MaxClassChips = 6;
    private static readonly TimeOnly[] QuickTimes = [new(8, 0), new(15, 0), DueInput.DefaultTime];

    private DeadlineService? _deadlines;
    private Deadline? _editing;

    public DeadlineForm()
    {
        InitializeComponent();
    }

    /// <summary>The form finished: the deadline was saved or deleted, or the user cancelled.</summary>
    public event EventHandler? Done;

    private static CultureInfo Culture => CultureInfo.CurrentCulture;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <param name="editing">The deadline to edit, or null to add a new one.</param>
    internal void Initialize(DeadlineService deadlines, Deadline? editing)
    {
        _deadlines = deadlines;
        _editing = editing;

        foreach (var name in deadlines.RecentClassNames(MaxClassChips))
        {
            var chip = new Button { Content = name, Style = (Style)FindResource("ChipButton") };
            chip.Click += (_, _) => { ClassBox.Text = name; ClassBox.CaretIndex = name.Length; };
            ClassChips.Children.Add(chip);
        }

        foreach (var time in QuickTimes)
        {
            var text = time.ToString("t", Culture);
            var chip = new Button { Content = text, Style = (Style)FindResource("ChipButton") };
            chip.Click += (_, _) => TimeBox.Text = text;
            TimeChips.Children.Add(chip);
        }

        if (editing is null)
        {
            SetDate(Today.AddDays(1));
            TimeBox.Text = DueInput.DefaultTime.ToString("t", Culture);
        }
        else
        {
            var local = editing.DueAt.ToLocalTime();
            Heading.Text = "Edit deadline";
            SaveButton.Content = "Save";
            DeleteButton.Visibility = Visibility.Visible;
            TitleBox.Text = editing.Title;
            ClassBox.Text = editing.ClassName ?? "";
            DescriptionBox.Text = editing.Description ?? "";
            SetDate(DateOnly.FromDateTime(local.DateTime));
            TimeBox.Text = TimeOnly.FromDateTime(local.DateTime).ToString("t", Culture);
        }

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    private void SetDate(DateOnly date) => DateBox.Text = date.ToDateTime(TimeOnly.MinValue).ToString("d", Culture);

    private void DateOrTime_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview(showErrors: false);

    /// <summary>Shows what the typed date means ("Friday, October 30") and whether it's in the past.</summary>
    private (DateOnly? Date, TimeOnly? Time) UpdatePreview(bool showErrors)
    {
        DateOnly? date = DueInput.TryParseDate(DateBox.Text, Today, Culture, out var d) ? d : null;
        TimeOnly? time = DueInput.TryParseTime(TimeBox.Text, Culture, out var t) ? t : null;

        DatePreview.Text = date is { } shown ? shown.ToDateTime(TimeOnly.MinValue).ToString("D", Culture) : "";
        if (date is not null) Calendar.SelectedDate = date;

        // Errors appear on Save, and disappear as soon as the text becomes valid.
        if (showErrors || date is not null) DateError.Visibility = date is null ? Visibility.Visible : Visibility.Collapsed;
        if (showErrors || time is not null) TimeError.Visibility = time is null ? Visibility.Visible : Visibility.Collapsed;

        PastNote.Visibility = date is { } dd && time is { } tt && DueInput.Combine(dd, tt) < DateTimeOffset.Now
            ? Visibility.Visible
            : Visibility.Collapsed;
        return (date, time);
    }

    private void DateChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string days }) SetDate(Today.AddDays(int.Parse(days, CultureInfo.InvariantCulture)));
    }

    private void CalendarToggle_Changed(object sender, RoutedEventArgs e) =>
        Calendar.Visibility = CalendarToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void Calendar_DateSelected(object? sender, DateOnly date)
    {
        SetDate(date);
        CalendarToggle.IsChecked = false;
        TimeBox.Focus();
    }

    private void Form_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        // Ctrl+Enter saves from anywhere. Plain Enter saves from a one-line text field; in the notes box it's
        // a new line, and on a button it presses that button.
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var inOneLineField = Keyboard.FocusedElement is TextBox box && !ReferenceEquals(box, DescriptionBox);
        if (ctrl || inOneLineField)
        {
            e.Handled = true;
            Save();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (_deadlines is null) return;

        var title = TitleBox.Text.Trim();
        TitleError.Visibility = title.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var (date, time) = UpdatePreview(showErrors: true);

        if (title.Length == 0) { TitleBox.Focus(); return; }
        if (date is null) { DateBox.Focus(); return; }
        if (time is null) { TimeBox.Focus(); return; }

        var dueAt = DueInput.Combine(date.Value, time.Value);
        if (_editing is null)
        {
            _deadlines.Add(title, ClassBox.Text, DescriptionBox.Text, dueAt);
        }
        else
        {
            _deadlines.Edit(_editing.Id, title, ClassBox.Text, DescriptionBox.Text, dueAt);
        }
        Done?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Done?.Invoke(this, EventArgs.Empty);

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_deadlines is null || _editing is null) return;

        var host = Window.GetWindow(this)!;
        MessageBoxResult confirm;
        using ((host as Flyouts.FlyoutWindow)?.KeepOpen())
        {
            confirm = MessageBox.Show(host, $"Delete \"{_editing.Title}\"?", "FocusBar",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        }
        if (confirm != MessageBoxResult.Yes) return;

        _deadlines.Delete(_editing.Id);
        Done?.Invoke(this, EventArgs.Empty);
    }
}
