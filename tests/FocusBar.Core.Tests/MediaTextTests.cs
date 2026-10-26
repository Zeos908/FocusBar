using FocusBar.Core.Media;

namespace FocusBar.Core.Tests;

public class MediaTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 16, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("Chrome", "Chrome")]
    [InlineData("MSEdge", "Microsoft Edge")]
    [InlineData("308046B0AF4A39CB", "Firefox")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe", "VLC")]
    [InlineData("SomeCompany.CoolPlayer", "CoolPlayer")]
    [InlineData("", "an app")]
    [InlineData(null, "an app")]
    public void App_names_read_well(string? id, string expected) => Assert.Equal(expected, MediaText.AppName(id));

    [Fact]
    public void Lines_are_title_and_artist()
    {
        Assert.Equal(("Blinding Lights", "The Weeknd"), MediaText.Lines("Blinding Lights", "The Weeknd", "After Hours", "Spotify"));
        Assert.Equal(("Lecture 4", "Biology"), MediaText.Lines(" Lecture 4 ", "", "Biology", "Chrome"));
        Assert.Equal(("Lofi beats", "Chrome"), MediaText.Lines("Lofi beats", null, null, "Chrome"));
        Assert.Equal(("Playing in Chrome", ""), MediaText.Lines(null, null, null, "Chrome"));
    }

    [Fact]
    public void Timeline_moves_forward_while_playing()
    {
        var view = MediaText.Timeline(TimeSpan.Zero, TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(60), Now.AddSeconds(-30), Now, playing: true);

        Assert.NotNull(view);
        Assert.Equal("1:30", view.Value.Elapsed);
        Assert.Equal("4:00", view.Value.Total);
        Assert.Equal(0.375, view.Value.Fraction, 6);
    }

    [Fact]
    public void Timeline_stays_put_while_paused_and_never_passes_the_end()
    {
        Assert.Equal("1:00", MediaText.Timeline(TimeSpan.Zero, TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(60), Now.AddMinutes(-10), Now, playing: false)!.Value.Elapsed);
        Assert.Equal(1, MediaText.Timeline(TimeSpan.Zero, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(3), Now.AddMinutes(-10), Now, playing: true)!.Value.Fraction);
    }

    [Fact]
    public void No_timeline_for_live_streams() =>
        Assert.Null(MediaText.Timeline(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(5), Now, Now, playing: true));

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(187, "3:07")]
    [InlineData(3765, "1:02:45")]
    public void Times_are_formatted_like_players_do(int seconds, string expected) =>
        Assert.Equal(expected, MediaText.FormatTime(TimeSpan.FromSeconds(seconds)));
}

public sealed class NowPlayingSettingTests : IDisposable
{
    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Now_playing_is_on_by_default_and_remembered()
    {
        var store = new FocusBar.Core.Settings.SettingsStore(new FocusBar.Core.Storage.JsonFileStore(_temp.Paths));
        Assert.True(store.Load().ShowNowPlaying);

        store.Save(store.Load() with { ShowNowPlaying = false });

        Assert.False(new FocusBar.Core.Settings.SettingsStore(new FocusBar.Core.Storage.JsonFileStore(_temp.Paths)).Load().ShowNowPlaying);
    }
}
