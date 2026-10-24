using FocusBar.Core.Status;

namespace FocusBar.Core.Tests;

public class StatusGlyphsTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "")]
    [InlineData(2, "")]
    [InlineData(3, "")]
    [InlineData(4, "")]
    [InlineData(5, "")]
    [InlineData(null, "")]
    public void Wifi_shows_signal_strength(int? bars, string expected)
    {
        Assert.Equal(expected, StatusGlyphs.Network(NetworkKind.Wifi, hasInternet: true, bars));
    }

    [Fact]
    public void Wifi_without_internet_shows_a_warning()
    {
        Assert.Equal(StatusGlyphs.WifiWarning4, StatusGlyphs.Network(NetworkKind.Wifi, hasInternet: false, 5));
    }

    [Fact]
    public void Ethernet_and_offline()
    {
        Assert.Equal(StatusGlyphs.Ethernet, StatusGlyphs.Network(NetworkKind.Ethernet, true, null));
        Assert.Equal(StatusGlyphs.EthernetWarning, StatusGlyphs.Network(NetworkKind.Ethernet, false, null));
        Assert.Equal(StatusGlyphs.NetworkOffline, StatusGlyphs.Network(NetworkKind.None, false, null));
    }

    [Fact]
    public void Network_tooltips()
    {
        Assert.Equal("HomeNet: Internet access", StatusGlyphs.NetworkTooltip(NetworkKind.Wifi, true, " HomeNet "));
        Assert.Equal("Wi-Fi: No internet", StatusGlyphs.NetworkTooltip(NetworkKind.Wifi, false, null));
        Assert.Equal("Not connected", StatusGlyphs.NetworkTooltip(NetworkKind.None, false, "ignored"));
    }

    [Theory]
    [InlineData(0.0, false, "")]
    [InlineData(0.2, false, "")]
    [InlineData(0.5, false, "")]
    [InlineData(1.0, false, "")]
    [InlineData(0.8, true, "")]
    public void Volume_levels(double level, bool muted, string expected)
    {
        Assert.Equal(expected, StatusGlyphs.Volume(level, muted));
    }

    [Fact]
    public void Volume_tooltip_rounds_to_a_whole_percent()
    {
        Assert.Equal("Volume: 46%", StatusGlyphs.VolumeTooltip(0.456, false));
        Assert.Equal("Volume: muted", StatusGlyphs.VolumeTooltip(0.456, true));
    }

    [Theory]
    [InlineData(0, false, 0xE850)]
    [InlineData(4, false, 0xE850)]
    [InlineData(5, false, 0xE851)]
    [InlineData(54, false, 0xE855)]
    [InlineData(94, false, 0xE859)]
    [InlineData(95, false, 0xE83F)]
    [InlineData(100, false, 0xE83F)]
    [InlineData(30, true, 0xE85D)]
    [InlineData(90, true, 0xE83E)]
    [InlineData(100, true, 0xEA93)]
    public void Battery_levels(int percent, bool charging, int expected)
    {
        Assert.Equal(char.ConvertFromUtf32(expected), StatusGlyphs.Battery(percent, charging));
    }

    [Fact]
    public void Unknown_battery()
    {
        Assert.Equal(StatusGlyphs.BatteryUnknown, StatusGlyphs.Battery(null, false));
        Assert.Equal("Battery: status unknown", StatusGlyphs.BatteryTooltip(null, false, false, null));
    }

    [Fact]
    public void Battery_tooltips()
    {
        Assert.Equal("Battery: 80% (charging)", StatusGlyphs.BatteryTooltip(80, true, true, null));
        Assert.Equal("Battery: 100% (plugged in)", StatusGlyphs.BatteryTooltip(100, true, false, null));
        Assert.Equal("Battery: 55% (2 hr 5 min left)", StatusGlyphs.BatteryTooltip(55, false, false, TimeSpan.FromMinutes(125)));
        Assert.Equal("Battery: 9% (12 min left)", StatusGlyphs.BatteryTooltip(9, false, false, TimeSpan.FromMinutes(12)));
        Assert.Equal("Battery: 9%", StatusGlyphs.BatteryTooltip(9, false, false, null));
    }
}
