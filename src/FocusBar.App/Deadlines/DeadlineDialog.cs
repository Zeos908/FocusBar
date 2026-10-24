using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Models;

namespace FocusBar.App.Deadlines;

/// <summary>The deadline form as a small dialog, used from the deadline list window.</summary>
public sealed class DeadlineDialog : Window
{
    internal DeadlineDialog(DeadlineService deadlines, Deadline? editing, Window owner)
    {
        Title = editing is null ? "Add a deadline" : "Edit deadline";
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "FlyoutBackground");
        SetResourceReference(ForegroundProperty, "FlyoutForeground");
        SetResourceReference(FontFamilyProperty, "BarFont");

        var form = new DeadlineForm { Margin = new Thickness(20) };
        form.Initialize(deadlines, editing);
        form.Done += (_, _) => Close();

        Content = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.WorkArea.Height - 80,
        };

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
    }
}
