using System.Globalization;
using FocusBar.Core.Deadlines;

namespace FocusBar.Core.Tests;

public class DueInputTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateOnly Saturday = new(2026, 10, 24); // Saturday, October 24, 2026

    private static DateOnly? Date(string text, CultureInfo? culture = null) =>
        DueInput.TryParseDate(text, Saturday, culture ?? Us, out var date) ? date : null;

    private static TimeOnly? Time(string text, CultureInfo? culture = null) =>
        DueInput.TryParseTime(text, culture ?? Us, out var time) ? time : null;

    [Theory]
    [InlineData("today", 2026, 10, 24)]
    [InlineData(" Tomorrow ", 2026, 10, 25)]
    [InlineData("tmrw", 2026, 10, 25)]
    [InlineData("mon", 2026, 10, 26)]
    [InlineData("Friday", 2026, 10, 30)]
    [InlineData("fri.", 2026, 10, 30)]
    [InlineData("tu", 2026, 10, 27)]
    [InlineData("th", 2026, 10, 29)]
    [InlineData("sat", 2026, 10, 31)] // a weekday name means the next one, not today
    [InlineData("10/31", 2026, 10, 31)]
    [InlineData("Oct 31", 2026, 10, 31)]
    [InlineData("November 3", 2026, 11, 3)]
    [InlineData("10/31/2027", 2027, 10, 31)]
    [InlineData("1/15", 2027, 1, 15)]       // already passed this year: next year
    [InlineData("1/15/26", 2026, 1, 15)]    // year typed: keep it, even in the past
    [InlineData("2026-12-01", 2026, 12, 1)]
    public void Parses_dates(string text, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), Date(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("t")]       // Tuesday or Thursday?
    [InlineData("s")]       // Saturday or Sunday?
    [InlineData("3pm")]     // a time, not a date
    [InlineData("someday")]
    [InlineData("13/45")]
    public void Rejects_what_is_not_a_date(string text)
    {
        Assert.Null(Date(text));
    }

    [Fact]
    public void Uses_the_regions_date_order()
    {
        var uk = CultureInfo.GetCultureInfo("en-GB");
        Assert.Equal(new DateOnly(2026, 11, 3), Date("3/11", uk));
        Assert.Equal(new DateOnly(2027, 3, 11), Date("3/11", Us));
    }

    [Theory]
    [InlineData("3pm", 15, 0)]
    [InlineData("3 PM", 15, 0)]
    [InlineData("3:30pm", 15, 30)]
    [InlineData("3:30 p.m.", 15, 30)]
    [InlineData("11:59 PM", 23, 59)]
    [InlineData("12pm", 12, 0)]
    [InlineData("12am", 0, 0)]
    [InlineData("8a", 8, 0)]
    [InlineData("15:30", 15, 30)]
    [InlineData("1530", 15, 30)]
    [InlineData("0800", 8, 0)]
    [InlineData("noon", 12, 0)]
    [InlineData("midnight", 23, 59)] // the end of the due date, not the start
    public void Parses_times(string text, int hour, int minute)
    {
        Assert.Equal(new TimeOnly(hour, minute), Time(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("13pm")]
    [InlineData("3:75")]
    [InlineData("0am")]
    [InlineData("later")]
    public void Rejects_what_is_not_a_time(string text)
    {
        Assert.Null(Time(text));
    }

    [Fact]
    public void Falls_back_to_the_regions_time_format()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal(new TimeOnly(14, 5), Time("14:05", german));
    }

    [Fact]
    public void Combine_uses_the_offset_for_that_date()
    {
        var eastern = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(-5), "Test", "Test", "Test",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2000, 1, 1), new DateTime(2099, 12, 31), TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)),
        ]);

        var summer = DueInput.Combine(new DateOnly(2026, 10, 30), new TimeOnly(23, 59), eastern);
        var winter = DueInput.Combine(new DateOnly(2026, 12, 4), new TimeOnly(23, 59), eastern);

        Assert.Equal(TimeSpan.FromHours(-4), summer.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), winter.Offset);
        Assert.Equal(new DateTime(2026, 10, 30, 23, 59, 0), summer.DateTime);
    }
}
