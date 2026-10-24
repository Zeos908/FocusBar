using System.Globalization;

namespace FocusBar.Core.Status;

public enum NetworkKind { None, Wifi, Ethernet, Cellular }

/// <summary>
/// Picks the icon (a Segoe Fluent Icons / Segoe MDL2 Assets glyph) and tooltip for the Wi-Fi, volume,
/// and battery indicators. Kept free of UI code so the choices can be unit-tested.
/// </summary>
public static class StatusGlyphs
{
    // Glyph names follow Microsoft's Segoe Fluent Icons reference.
    public const string Wifi = "";
    public const string Wifi1 = "";
    public const string Wifi2 = "";
    public const string Wifi3 = "";
    public const string WifiWarning4 = "";
    public const string Ethernet = "";
    public const string EthernetWarning = "";
    public const string Cellular = "";
    public const string NetworkOffline = "";

    public const string Mute = "";
    public const string Volume0 = "";
    public const string Volume1 = "";
    public const string Volume2 = "";
    public const string Volume3 = "";

    public const string BatteryUnknown = "";
    private const int Battery0 = 0xE850;          // Battery0..Battery9 are E850..E859
    private const int Battery10 = 0xE83F;
    private const int BatteryCharging0 = 0xE85A;  // BatteryCharging0..8 are E85A..E862
    private const int BatteryCharging9 = 0xE83E;
    private const int BatteryCharging10 = 0xEA93;

    /// <param name="signalBars">0 to 5, as Windows reports it; null if unknown.</param>
    public static string Network(NetworkKind kind, bool hasInternet, int? signalBars) => kind switch
    {
        NetworkKind.Wifi when !hasInternet => WifiWarning4,
        NetworkKind.Wifi => (signalBars ?? 5) switch
        {
            <= 1 => Wifi1,
            2 => Wifi2,
            3 => Wifi3,
            _ => Wifi,
        },
        NetworkKind.Ethernet => hasInternet ? Ethernet : EthernetWarning,
        NetworkKind.Cellular => Cellular,
        _ => NetworkOffline,
    };

    public static string NetworkTooltip(NetworkKind kind, bool hasInternet, string? networkName)
    {
        var name = string.IsNullOrWhiteSpace(networkName) ? null : networkName.Trim();
        var access = hasInternet ? "Internet access" : "No internet";
        return kind switch
        {
            NetworkKind.Wifi => name is null ? $"Wi-Fi: {access}" : $"{name}: {access}",
            NetworkKind.Ethernet => $"Ethernet: {access}",
            NetworkKind.Cellular => name is null ? $"Cellular: {access}" : $"{name}: {access}",
            _ => "Not connected",
        };
    }

    /// <param name="level">0.0 to 1.0.</param>
    public static string Volume(double level, bool muted)
    {
        if (muted) return Mute;
        var percent = ToPercent(level);
        return percent switch
        {
            0 => Volume0,
            < 34 => Volume1,
            < 67 => Volume2,
            _ => Volume3,
        };
    }

    public static string VolumeTooltip(double level, bool muted) =>
        muted ? "Volume: muted" : $"Volume: {ToPercent(level).ToString(CultureInfo.CurrentCulture)}%";

    /// <param name="percent">0 to 100, or null if Windows can't tell.</param>
    public static string Battery(int? percent, bool charging)
    {
        if (percent is null) return BatteryUnknown;

        var step = (int)Math.Round(Math.Clamp(percent.Value, 0, 100) / 10.0, MidpointRounding.AwayFromZero);
        var code = (charging, step) switch
        {
            (false, 10) => Battery10,
            (false, _) => Battery0 + step,
            (true, 10) => BatteryCharging10,
            (true, 9) => BatteryCharging9,
            (true, _) => BatteryCharging0 + step,
        };
        return char.ConvertFromUtf32(code);
    }

    public static string BatteryTooltip(int? percent, bool pluggedIn, bool charging, TimeSpan? remaining)
    {
        if (percent is null) return "Battery: status unknown";

        var text = $"Battery: {percent.Value.ToString(CultureInfo.CurrentCulture)}%";
        if (charging) return text + " (charging)";
        if (pluggedIn) return text + " (plugged in)";
        if (remaining is { } left && left > TimeSpan.Zero)
        {
            text += left.TotalHours >= 1
                ? $" ({(int)left.TotalHours} hr {left.Minutes} min left)"
                : $" ({left.Minutes} min left)";
        }
        return text;
    }

    private static int ToPercent(double level) =>
        (int)Math.Round(Math.Clamp(level, 0, 1) * 100, MidpointRounding.AwayFromZero);
}
