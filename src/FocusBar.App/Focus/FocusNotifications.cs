using FocusBar.Core.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FocusBar.App.Focus;

/// <summary>Windows notifications for Focus Mode starting and ending.</summary>
internal static class FocusNotifications
{
    private const string Tag = "focus";
    private const string Group = "focus";

    public static void Show(string title, string body, ErrorLog log)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "focus")
                .AddText(title)
                .AddText(body)
                .AddAttributionText("FocusBar")
                .Show(t =>
                {
                    t.Tag = Tag;
                    t.Group = Group;
                });
        }
        catch (Exception ex)
        {
            log.Write(ex, "Could not show a Focus Mode notification.");
        }
    }
}
