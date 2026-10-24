using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.Core.Tests;

public class UrgencyRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.FromHours(-4));

    [Theory]
    [InlineData(24 * 7, Urgency.Low)]        // a week out
    [InlineData(24 * 3 + 1, Urgency.Low)]    // just over 3 days
    [InlineData(24 * 3, Urgency.Medium)]     // exactly 3 days
    [InlineData(24, Urgency.Medium)]         // exactly 1 day
    [InlineData(23, Urgency.High)]           // under 24 hours
    [InlineData(0.01, Urgency.High)]         // seconds left
    public void Classifies_by_time_remaining(double hoursLeft, Urgency expected)
    {
        Assert.Equal(expected, UrgencyRules.Classify(Now.AddHours(hoursLeft), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-24 * 30)]
    public void Due_now_or_past_is_overdue(double hoursLeft)
    {
        Assert.Equal(Urgency.Overdue, UrgencyRules.Classify(Now.AddHours(hoursLeft), Now));
    }

    [Fact]
    public void Completed_deadline_is_never_overdue()
    {
        var done = new Deadline(Guid.NewGuid(), "Bio lab", "Biology", null, Now.AddDays(-1), DeadlineSource.Manual, null, IsComplete: true);
        Assert.Equal(Urgency.Low, UrgencyRules.Classify(done, Now));
    }

    [Fact]
    public void Incomplete_deadline_uses_due_time()
    {
        var pending = new Deadline(Guid.NewGuid(), "Essay", "English", null, Now.AddHours(5), DeadlineSource.Manual, null, IsComplete: false);
        Assert.Equal(Urgency.High, UrgencyRules.Classify(pending, Now));
    }

    [Fact]
    public void Time_zones_do_not_change_the_result()
    {
        var dueUtc = Now.ToUniversalTime().AddHours(30);
        Assert.Equal(Urgency.Medium, UrgencyRules.Classify(dueUtc, Now));
    }
}
