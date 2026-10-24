using FocusBar.Core.Deadlines;

namespace FocusBar.Core.Tests;

public class CountdownTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2 * 24 * 60 + 4 * 60 + 30, "2d 4h")]
    [InlineData(3 * 24 * 60, "3d")]
    [InlineData(23 * 60 + 59, "23h 59m")]
    [InlineData(119, "1h 59m")]
    [InlineData(60, "1h")]
    [InlineData(45, "45m")]
    [InlineData(1, "1m")]
    public void Formats_time_left_rounding_down(int minutesLeft, string expected)
    {
        Assert.Equal(expected, Countdown.Format(Now.AddMinutes(minutesLeft), Now));
    }

    [Fact]
    public void Under_a_minute()
    {
        Assert.Equal("<1m", Countdown.Format(Now.AddSeconds(30), Now));
        Assert.Equal("due now", Countdown.Format(Now, Now));
        Assert.Equal("due now", Countdown.Format(Now.AddSeconds(-30), Now));
    }

    [Theory]
    [InlineData(-90, "1h 30m overdue")]
    [InlineData(-3 * 24 * 60, "3d overdue")]
    [InlineData(-5, "5m overdue")]
    public void Overdue(int minutesLeft, string expected)
    {
        Assert.Equal(expected, Countdown.Format(Now.AddMinutes(minutesLeft), Now));
    }

    [Theory]
    [InlineData(2 * 24 * 60 + 4 * 60, "in 2 days, 4 hours")]
    [InlineData(24 * 60, "in 1 day")]
    [InlineData(61, "in 1 hour, 1 minute")]
    [InlineData(30, "in 30 minutes")]
    [InlineData(-120, "2 hours ago")]
    public void Describes_in_words(int minutesLeft, string expected)
    {
        Assert.Equal(expected, Countdown.Describe(Now.AddMinutes(minutesLeft), Now));
    }

    [Fact]
    public void Works_across_time_zones()
    {
        var dueInTokyo = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(9)); // = Oct 25 00:00 UTC
        Assert.Equal("9h", Countdown.Format(dueInTokyo, Now));
    }
}
