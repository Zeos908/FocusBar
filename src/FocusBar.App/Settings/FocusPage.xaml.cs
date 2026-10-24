using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Focus;
using FocusBar.App.Services;
using FocusBar.Core.Focus;
using FocusBar.Core.Models;
using FocusBar.Core.Reminders;

namespace FocusBar.App.Settings;

/// <summary>Settings → Focus Mode: when it turns on, how strict it is, and what it blocks.</summary>
public partial class FocusPage : UserControl
{
    private static readonly TimeSpan[] LeadTimes =
        [TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6)];

    private static readonly TimeSpan[] OverrideDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];

    private readonly SettingsService _settings;
    private readonly AppCatalog _apps;
    private readonly FocusController _focus;
    private bool _loading = true;

    internal FocusPage(SettingsService settings, AppCatalog apps, FocusController controller)
    {
        _settings = settings;
        _apps = apps;
        _focus = controller;
        InitializeComponent();

        controller.Changed += OnFocusChanged;
        Unloaded += (_, _) => controller.Changed -= OnFocusChanged;

        var focus = settings.Current.Focus;
        AutoBox.IsChecked = focus.AutoEnable;
        BuildChips(LeadTimes, focus.StartBeforeDeadline, LeadTimeChips, "LeadTime", t => ReminderSettings.Describe(t),
            t => Update(f => f with { StartBeforeDeadline = t }));
        BuildChips(OverrideDelays, focus.SoftOverrideDelay, OverrideChips, "Override",
            t => t < TimeSpan.FromMinutes(1) ? $"{t.TotalSeconds:0} seconds" : ReminderSettings.Describe(t),
            t => Update(f => f with { SoftOverrideDelay = t }));

        (focus.Strictness switch
        {
            Strictness.Gentle => GentleOption,
            Strictness.Strict => StrictOption,
            _ => SoftOption,
        }).IsChecked = true;

        _loading = false;
        Refresh();
        UpdateLock();
    }

    private void OnFocusChanged(object? sender, EventArgs e) => UpdateLock();

    /// <summary>
    /// During a Strict session the settings can't loosen it (otherwise Strict would just be Soft with extra steps).
    /// Exiting FocusBar still ends it; nothing here fights the student.
    /// </summary>
    private void UpdateLock()
    {
        var locked = _focus.Active is not null && !_focus.CanEnd;
        Editable.IsEnabled = !locked;
        LockedBanner.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        if (locked)
        {
            LockedText.Text = $"Strict Focus Mode is on until {_focus.Active!.EndsAt.ToLocalTime():t}. " +
                              "You can change these settings after it ends.";
        }
    }

    private FocusSettings Current => _settings.Current.Focus;

    private void Update(Func<FocusSettings, FocusSettings> change)
    {
        if (_loading) return;
        _settings.Update(s => s with { Focus = BlockList.Clean(change(s.Focus)) });
        Refresh();
    }

    private void Refresh()
    {
        var focus = Current;
        foreach (var chip in LeadTimeChips.Children.OfType<RadioButton>()) chip.IsEnabled = focus.AutoEnable;
        foreach (var chip in OverrideChips.Children.OfType<RadioButton>()) chip.IsEnabled = focus.Strictness == Strictness.Soft;

        AppList.ItemsSource = focus.BlockedApps.ToList();
        NoAppsText.Visibility = focus.BlockedApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SiteList.ItemsSource = focus.BlockedSites.ToList();
        NoSitesText.Visibility = focus.BlockedSites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SuggestionChips.Children.Clear();
        foreach (var site in BlockList.SuggestedSites.Where(s => !focus.BlockedSites.Contains(s)))
        {
            var chip = new Button { Content = "+ " + site, Style = (Style)FindResource("ChipButton") };
            chip.Click += (_, _) => Update(f => f with { BlockedSites = [.. f.BlockedSites, site] });
            SuggestionChips.Children.Add(chip);
        }
    }

    private void BuildChips(TimeSpan[] values, TimeSpan current, Panel panel, string group, Func<TimeSpan, string> label, Action<TimeSpan> onPick)
    {
        foreach (var value in values)
        {
            var chip = new RadioButton
            {
                Content = label(value),
                GroupName = group,
                Style = (Style)FindResource("ChipRadio"),
                IsChecked = value == current,
            };
            chip.Checked += (_, _) => onPick(value);
            panel.Children.Add(chip);
        }
    }

    private void AutoBox_Changed(object sender, RoutedEventArgs e)
    {
        var on = AutoBox.IsChecked == true;
        Update(f => f with { AutoEnable = on });
    }

    private void Strictness_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<Strictness>(tag, out var strictness))
        {
            Update(f => f with { Strictness = strictness });
        }
    }

    // ----- Apps -----

    private void PickApp_Click(object sender, RoutedEventArgs e)
    {
        var picker = new AppPickerDialog(_apps, Window.GetWindow(this)!);
        if (picker.ShowDialog() == true && picker.ChosenProcessName is { } name) AddApp(name);
    }

    private void AddAppByName_Click(object sender, RoutedEventArgs e)
    {
        if (AddApp(AppNameBox.Text)) AppNameBox.Clear();
    }

    private bool AddApp(string input)
    {
        var name = BlockList.NormalizeAppName(input);
        var problem = name switch
        {
            null => "Type the program's name, like Discord or Discord.exe.",
            _ when BlockList.IsProtected(name) => $"{name} is part of Windows or FocusBar and can't be blocked.",
            _ when Current.BlockedApps.Contains(name, StringComparer.OrdinalIgnoreCase) => $"{name} is already on the list.",
            _ when Current.BlockedApps.Count >= BlockList.MaxEntries => "The list is full.",
            _ => null,
        };

        AppError.Text = problem ?? "";
        AppError.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        if (problem is not null) return false;

        Update(f => f with { BlockedApps = [.. f.BlockedApps, name!] });
        return true;
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string name)
        {
            Update(f => f with { BlockedApps = [.. f.BlockedApps.Where(a => !a.Equals(name, StringComparison.OrdinalIgnoreCase))] });
        }
    }

    // ----- Websites -----

    private void AddSite_Click(object sender, RoutedEventArgs e)
    {
        var domain = HostsFile.NormalizeDomain(SiteBox.Text);
        var problem = domain switch
        {
            null => "Type a website like youtube.com.",
            _ when Current.BlockedSites.Contains(domain) => $"{domain} is already on the list.",
            _ when Current.BlockedSites.Count >= BlockList.MaxEntries => "The list is full.",
            _ => null,
        };

        SiteError.Text = problem ?? "";
        SiteError.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        if (problem is not null) return;

        Update(f => f with { BlockedSites = [.. f.BlockedSites, domain!] });
        SiteBox.Clear();
    }

    private void RemoveSite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string site)
        {
            Update(f => f with { BlockedSites = [.. f.BlockedSites.Where(s => s != site)] });
        }
    }
}
