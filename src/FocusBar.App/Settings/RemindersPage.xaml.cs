using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Services;
using FocusBar.Core.Reminders;

namespace FocusBar.App.Settings;

/// <summary>Settings → Reminders: on/off, how long before each deadline, and a test button.</summary>
public partial class RemindersPage : UserControl
{
    private readonly SettingsService _settings;
    private readonly ReminderService _reminders;
    private readonly List<(TimeSpan Offset, CheckBox Box)> _offsetBoxes = [];
    private bool _loading = true;

    internal RemindersPage(SettingsService settings, ReminderService reminders)
    {
        _settings = settings;
        _reminders = reminders;
        InitializeComponent();

        var current = settings.Current.Reminders;
        EnabledBox.IsChecked = current.Enabled;

        foreach (var offset in ReminderSettings.Choices)
        {
            var box = new CheckBox
            {
                Content = $"{ReminderSettings.Describe(offset)} before",
                IsChecked = current.Offsets.Contains(offset),
                Style = (Style)FindResource("FlyoutCheckBox"),
            };
            box.Checked += (_, _) => SaveOffsets();
            box.Unchecked += (_, _) => SaveOffsets();
            _offsetBoxes.Add((offset, box));
            OffsetBoxes.Children.Add(box);
        }

        UpdateEnabledState();
        _loading = false;
    }

    private void EnabledBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var enabled = EnabledBox.IsChecked == true;
        _settings.Update(s => s with { Reminders = s.Reminders with { Enabled = enabled } });
        UpdateEnabledState();
    }

    private void SaveOffsets()
    {
        if (_loading) return;
        var chosen = _offsetBoxes.Where(x => x.Box.IsChecked == true).Select(x => x.Offset).ToList();
        _settings.Update(s => s with { Reminders = s.Reminders with { Offsets = chosen } });
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        var enabled = EnabledBox.IsChecked == true;
        foreach (var (_, box) in _offsetBoxes) box.IsEnabled = enabled;
        NoneChosenText.Visibility = enabled && _offsetBoxes.All(x => x.Box.IsChecked != true)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TestButton_Click(object sender, RoutedEventArgs e) => _reminders.ShowTest();
}
