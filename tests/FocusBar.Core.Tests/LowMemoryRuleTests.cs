using FocusBar.Core.Safety;
using FocusBar.Core.Settings;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class LowMemoryRuleTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 15, 0, 0, TimeSpan.FromHours(-4));
    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Closes_after_memory_stays_above_the_threshold_for_30_seconds()
    {
        var rule = new LowMemoryRule();

        Assert.False(rule.Observe(93, 90, Start));
        Assert.False(rule.Observe(95, 90, Start.AddSeconds(15)));
        Assert.False(rule.Observe(92, 90, Start.AddSeconds(29)));
        Assert.True(rule.Observe(91, 90, Start.AddSeconds(30)));
    }

    [Fact]
    public void A_short_spike_does_not_close_it()
    {
        var rule = new LowMemoryRule();

        Assert.False(rule.Observe(97, 90, Start));
        Assert.False(rule.Observe(96, 90, Start.AddSeconds(20)));
        Assert.False(rule.Observe(70, 90, Start.AddSeconds(25))); // dropped: start over
        Assert.Null(rule.HighSince);
        Assert.False(rule.Observe(95, 90, Start.AddSeconds(30)));
        Assert.False(rule.Observe(95, 90, Start.AddSeconds(55)));
        Assert.True(rule.Observe(95, 90, Start.AddSeconds(60)));
    }

    [Fact]
    public void Exactly_at_the_threshold_is_not_above_it()
    {
        var rule = new LowMemoryRule();
        Assert.False(rule.Observe(90, 90, Start));
        Assert.False(rule.Observe(90, 90, Start.AddMinutes(5)));
    }

    [Fact]
    public void Unknown_readings_and_reset_start_over()
    {
        var rule = new LowMemoryRule();
        rule.Observe(99, 90, Start);
        rule.Observe(double.NaN, 90, Start.AddSeconds(10));
        Assert.Null(rule.HighSince);

        rule.Observe(99, 90, Start.AddSeconds(20));
        rule.Reset();
        Assert.False(rule.Observe(99, 90, Start.AddSeconds(45)));
    }

    [Theory]
    [InlineData(16UL, 4UL, 75.0)]
    [InlineData(8UL, 0UL, 100.0)]
    [InlineData(8UL, 8UL, 0.0)]
    public void Used_percent_from_total_and_available(ulong totalGb, ulong availableGb, double expected) =>
        Assert.Equal(expected, LowMemoryRule.UsedPercent(totalGb << 30, availableGb << 30), 6);

    [Fact]
    public void Used_percent_is_unknown_for_impossible_numbers()
    {
        Assert.True(double.IsNaN(LowMemoryRule.UsedPercent(0, 0)));
        Assert.True(double.IsNaN(LowMemoryRule.UsedPercent(4, 8)));
    }

    [Fact]
    public void Setting_defaults_to_on_at_90_percent_and_is_saved()
    {
        var store = new SettingsStore(new JsonFileStore(_temp.Paths));
        Assert.Equal(new LowMemorySettings(true, 90), store.Load().LowMemory);

        store.Save(store.Load() with { LowMemory = new LowMemorySettings(false, 85) });
        Assert.Equal(new LowMemorySettings(false, 85), new SettingsStore(new JsonFileStore(_temp.Paths)).Load().LowMemory);
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(150, 90)]
    [InlineData(10, 90)]
    [InlineData(95, 95)]
    public void Odd_thresholds_go_back_to_90(int saved, int expected) =>
        Assert.Equal(expected, LowMemorySettings.Clean(new LowMemorySettings(true, saved)).ThresholdPercent);

    [Fact]
    public void Missing_section_gets_the_defaults() => Assert.Equal(LowMemorySettings.Default, LowMemorySettings.Clean(null));
}
