using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.Core.Models;

namespace FocusBar.App.Deadlines;

/// <summary>The deadline form in a flyout above the bar: the ➕ button's quick add, and Edit from the ticker.</summary>
public sealed class DeadlineFormFlyout : FlyoutWindow
{
    internal DeadlineFormFlyout(DeadlineService deadlines, Deadline? editing = null)
    {
        Title = editing is null ? "Add a deadline" : "Edit deadline";

        var form = new DeadlineForm { Margin = new Thickness(16) };
        form.Initialize(deadlines, editing);
        form.Done += (_, _) => CloseFlyout();

        // Scrolls rather than running off the top of a small screen when the date picker is open.
        var scroller = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.WorkArea.Height - 40,
        };
        var chrome = new Border { Child = scroller };
        chrome.SetResourceReference(StyleProperty, "FlyoutChrome");
        Content = chrome;
    }
}
