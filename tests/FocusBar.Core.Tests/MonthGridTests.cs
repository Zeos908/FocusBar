using FocusBar.Core.Status;

namespace FocusBar.Core.Tests;

public class MonthGridTests
{
    [Fact]
    public void Always_six_weeks()
    {
        Assert.Equal(42, MonthGrid.Build(2026, 2, DayOfWeek.Sunday).Count);
    }

    [Fact]
    public void Starts_on_the_first_day_of_the_week_before_the_1st()
    {
        // October 1, 2026 is a Thursday.
        var sundayFirst = MonthGrid.Build(2026, 10, DayOfWeek.Sunday);
        Assert.Equal(new DateOnly(2026, 9, 27), sundayFirst[0]);
        Assert.Equal(DayOfWeek.Sunday, sundayFirst[0].DayOfWeek);

        var mondayFirst = MonthGrid.Build(2026, 10, DayOfWeek.Monday);
        Assert.Equal(new DateOnly(2026, 9, 28), mondayFirst[0]);
    }

    [Fact]
    public void Month_starting_on_the_first_weekday_has_no_leading_days()
    {
        // November 1, 2026 is a Sunday.
        Assert.Equal(new DateOnly(2026, 11, 1), MonthGrid.Build(2026, 11, DayOfWeek.Sunday)[0]);
    }

    [Fact]
    public void Dates_are_consecutive_and_cross_the_year_end()
    {
        var days = MonthGrid.Build(2026, 12, DayOfWeek.Sunday);
        for (var i = 1; i < days.Count; i++)
        {
            Assert.Equal(days[i - 1].AddDays(1), days[i]);
        }
        Assert.Contains(new DateOnly(2027, 1, 2), days);
    }
}
