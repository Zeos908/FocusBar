using System.Globalization;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.App.Ticker;

/// <summary>Shared wording and colors for showing a deadline in the ticker, details, and list.</summary>
internal static class DeadlineDisplay
{
    /// <summary>The theme brush for an urgency level (defined in Themes/Dark.xaml and Light.xaml).</summary>
    public static string BrushKey(Urgency urgency) => urgency switch
    {
        Urgency.Low => "UrgencyLow",
        Urgency.Medium => "UrgencyMedium",
        Urgency.High => "UrgencyHigh",
        _ => "UrgencyOverdue",
    };

    /// <summary>"Fri, Oct 30, 11:59 PM" in the user's region format.</summary>
    public static string ShortDue(Deadline deadline)
    {
        var local = deadline.DueAt.ToLocalTime().DateTime;
        var culture = CultureInfo.CurrentCulture;
        // The region's month/day order with a short month name: "Oct 30" in the US, "30 Oct" in the UK.
        var monthDay = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM");
        return $"{local.ToString("ddd", culture)}, {local.ToString(monthDay, culture)}, {local.ToString("t", culture)}";
    }

    /// <summary>"Friday, October 30, 2026 at 11:59 PM".</summary>
    public static string LongDue(Deadline deadline)
    {
        var local = deadline.DueAt.ToLocalTime().DateTime;
        var culture = CultureInfo.CurrentCulture;
        return $"{local.ToString("D", culture)} at {local.ToString("t", culture)}";
    }

    public static string SourceText(DeadlineSource source) => source switch
    {
        DeadlineSource.Manual => "Added by you",
        DeadlineSource.Calendar => "From your calendar",
        DeadlineSource.SchoolApi => "From your school",
        _ => "",
    };

    /// <summary>Screen-reader and tooltip text: "Bio lab, Biology, due Fri, Oct 30, 11:59 PM, in 2 days, 4 hours".</summary>
    public static string Summary(Deadline deadline, DateTimeOffset now)
    {
        var parts = new List<string> { deadline.Title };
        if (!string.IsNullOrWhiteSpace(deadline.ClassName)) parts.Add(deadline.ClassName);
        parts.Add($"due {ShortDue(deadline)}");
        parts.Add(Countdown.Describe(deadline.DueAt, now));
        return string.Join(", ", parts);
    }
}
