using FocusBar.Core.Appearance;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public class ColorMathTests
{
    [Theory]
    [InlineData("#4f8ef7", "#4F8EF7")]
    [InlineData("4F8EF7", "#4F8EF7")]
    [InlineData("  #abc ", "#AABBCC")]
    [InlineData("#000000", "#000000")]
    public void Normalizes_valid_colors(string input, string expected) => Assert.Equal(expected, ColorMath.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#FF4F8EF7")]
    [InlineData("blue")]
    public void Rejects_anything_else(string? input) => Assert.Null(ColorMath.Normalize(input));

    [Fact]
    public void Contrast_matches_wcag()
    {
        Assert.Equal(21, ColorMath.Contrast(ColorMath.Black, ColorMath.White), 2);
        Assert.Equal(1, ColorMath.Contrast(ColorMath.White, ColorMath.White), 2);
    }

    [Theory]
    [InlineData("#17132E", true)]  // Midnight
    [InlineData("#1F1F23", true)]
    [InlineData("#FFF1E0", false)] // Sunrise
    [InlineData("#FFFFFF", false)]
    [InlineData("#FFD400", false)] // bright yellow wants black text
    [InlineData("#2563D9", true)]  // blue wants white text
    public void Picks_white_text_only_for_dark_colors(string hex, bool dark)
    {
        Assert.True(ColorMath.TryParse(hex, out var color));
        Assert.Equal(dark, ColorMath.IsDark(color));
    }
}

public class ImageInfoTests
{
    internal static byte[] Png(int width, int height)
    {
        var bytes = new List<byte> { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 };
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange(new byte[] { 8, 6, 0, 0, 0, 1, 2, 3, 4 }); // rest of IHDR + a fake CRC
        return [.. bytes];
    }

    internal static byte[] Gif(int width, int height) =>
        [.. "GIF89a"u8.ToArray(), (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0];

    /// <summary>SOI, an APP0 (JFIF) segment, a DQT segment, then SOF0 with the size.</summary>
    internal static byte[] Jpeg(int width, int height, byte sofMarker = 0xC0) =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0,
        0xFF, 0xDB, 0x00, 0x04, 0x00, 0x01,
        0xFF, sofMarker, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03,
        1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1,
        0xFF, 0xD9,
    ];

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static ImageInfo? Read(byte[] bytes) => ImageInfo.TryRead(new MemoryStream(bytes));

    [Fact]
    public void Reads_png_size() => Assert.Equal(new ImageInfo(ImageFormat.Png, 1920, 1080), Read(Png(1920, 1080)));

    [Fact]
    public void Reads_gif_size() => Assert.Equal(new ImageInfo(ImageFormat.Gif, 300, 20), Read(Gif(300, 20)));

    [Fact]
    public void Reads_jpeg_size_after_other_segments() => Assert.Equal(new ImageInfo(ImageFormat.Jpeg, 4032, 3024), Read(Jpeg(4032, 3024)));

    [Fact]
    public void Reads_progressive_jpeg_size() => Assert.Equal(new ImageInfo(ImageFormat.Jpeg, 640, 480), Read(Jpeg(640, 480, sofMarker: 0xC2)));

    [Fact]
    public void Extension_comes_from_the_contents()
    {
        Assert.Equal(".png", Read(Png(1, 1))!.Value.Extension);
        Assert.Equal(".jpg", Read(Jpeg(1, 1))!.Value.Extension);
        Assert.Equal(".gif", Read(Gif(1, 1))!.Value.Extension);
    }

    [Theory]
    [InlineData("not an image at all")]
    [InlineData("BM fake bitmap header..")]
    [InlineData("GIF")]
    [InlineData("")]
    public void Rejects_other_files(string text) => Assert.Null(Read(System.Text.Encoding.ASCII.GetBytes(text)));

    [Fact]
    public void Rejects_zero_size_and_truncated_images()
    {
        Assert.Null(Read(Png(0, 10)));
        Assert.Null(Read(Png(100, 100)[..18]));
        Assert.Null(Read(Jpeg(100, 100)[..30]));
        Assert.Null(Read([0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02, 0, 0, 0, 0, 0])); // scan before any frame header
    }
}

public class ImageLayoutTests
{
    [Fact]
    public void Fill_scales_to_cover_the_bar_and_keeps_only_the_middle_strip()
    {
        // A 1920×1080 photo on a 1920×48 bar at 100%: scale 1:1, keep the middle 48 rows.
        var plan = ImageLayout.Plan(1920, 1080, 1920, 48, 1.0, ImageFit.Fill);

        Assert.Equal((1920, 1080), (plan.DecodeWidth, plan.DecodeHeight));
        Assert.Equal((0, 516, 1920, 48), (plan.CropX, plan.CropY, plan.CropWidth, plan.CropHeight));
        Assert.Equal((1920.0, 48.0), (plan.DisplayWidth, plan.DisplayHeight));
    }

    [Theory]
    [InlineData(1.0, 1920, 48)]
    [InlineData(1.25, 2400, 60)]
    [InlineData(1.5, 2880, 72)]
    public void Fill_uses_real_screen_pixels_at_each_scaling_level(double scale, int pxWidth, int pxHeight)
    {
        // The bar is 1920×48 in WPF units at every scale; the screen has more pixels at higher scales.
        var plan = ImageLayout.Plan(3840, 2160, 1920, 48, scale, ImageFit.Fill);

        Assert.Equal(pxWidth, plan.DecodeWidth);
        Assert.Equal((pxWidth, pxHeight), (plan.CropWidth, plan.CropHeight));
        Assert.Equal(1920, plan.DisplayWidth, 6);
        Assert.Equal(48, plan.DisplayHeight, 6);
    }

    [Fact]
    public void Fill_with_a_small_image_scales_it_up_to_cover()
    {
        var plan = ImageLayout.Plan(200, 100, 1920, 48, 1.0, ImageFit.Fill);

        Assert.Equal((1920, 960), (plan.DecodeWidth, plan.DecodeHeight));
        Assert.Equal((1920, 48), (plan.CropWidth, plan.CropHeight));
    }

    [Fact]
    public void Fit_shows_the_whole_image_at_bar_height()
    {
        var plan = ImageLayout.Plan(1920, 1080, 1920, 48, 1.5, ImageFit.Fit);

        Assert.Equal(72, plan.DecodeHeight);
        Assert.Equal(128, plan.DecodeWidth);
        Assert.Equal((0, 0, 128, 72), (plan.CropX, plan.CropY, plan.CropWidth, plan.CropHeight));
        Assert.Equal(48, plan.DisplayHeight, 6);
    }

    [Fact]
    public void Center_keeps_real_size_and_crops_to_the_bar()
    {
        var plan = ImageLayout.Plan(400, 300, 1920, 48, 1.25, ImageFit.Center);

        Assert.Equal((400, 300), (plan.DecodeWidth, plan.DecodeHeight));
        Assert.Equal((0, 120, 400, 60), (plan.CropX, plan.CropY, plan.CropWidth, plan.CropHeight));
        Assert.Equal(320, plan.DisplayWidth, 6); // 400 pixels is 320 WPF units at 125%
    }

    [Fact]
    public void Never_decodes_absurdly_large()
    {
        var plan = ImageLayout.Plan(10, 10_000, 1920, 48, 1.0, ImageFit.Fill);
        Assert.True(plan.DecodeHeight <= ImageLayout.MaxDecodeSide);
        Assert.True(plan.DecodeWidth >= 1);
    }

    [Fact]
    public void Rejects_empty_images() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageLayout.Plan(0, 10, 100, 10, 1, ImageFit.Fill));
}

public sealed class ThemeImagesTests : IDisposable
{
    private readonly TempAppData _temp = new();
    private readonly ThemeImages _images;
    private readonly string _sourceFolder;

    public ThemeImagesTests()
    {
        _images = new ThemeImages(_temp.Paths);
        _sourceFolder = Path.Combine(_temp.Paths.Root, "originals");
        Directory.CreateDirectory(_sourceFolder);
    }

    public void Dispose() => _temp.Dispose();

    private string Source(string name, byte[] bytes)
    {
        var path = Path.Combine(_sourceFolder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Import_copies_the_image_so_the_original_can_move()
    {
        var source = Source("my photo.PNG", ImageInfoTests.Png(1920, 1080));

        var name = _images.Import(source, ThemeImageKind.Background);
        File.Delete(source);

        Assert.Matches(@"^background-[0-9a-f]{16}\.png$", name);
        Assert.True(ThemeImages.IsValidName(name));
        Assert.NotNull(_images.Resolve(name));
        Assert.Equal(Path.Combine(_temp.Paths.Images, name), _images.Resolve(name));
    }

    [Fact]
    public void Same_image_twice_is_stored_once()
    {
        var a = _images.Import(Source("a.png", ImageInfoTests.Png(10, 10)), ThemeImageKind.StartIcon);
        var b = _images.Import(Source("b.png", ImageInfoTests.Png(10, 10)), ThemeImageKind.StartIcon);

        Assert.Equal(a, b);
        Assert.Single(Directory.GetFiles(_temp.Paths.Images));
    }

    [Fact]
    public void Extension_follows_the_real_format_not_the_file_name()
    {
        var name = _images.Import(Source("actually-a-gif.png", ImageInfoTests.Gif(64, 64)), ThemeImageKind.FocusScreen);
        Assert.Matches(@"^focus-[0-9a-f]{16}\.gif$", name);
    }

    [Fact]
    public void Refuses_files_that_are_not_images()
    {
        var error = Assert.Throws<ThemeImageException>(() => _images.Import(Source("virus.png", "MZ not an image"u8.ToArray()), ThemeImageKind.Background));
        Assert.Contains("PNG, JPG, or GIF", error.Message);
        Assert.False(Directory.Exists(_temp.Paths.Images) && Directory.GetFiles(_temp.Paths.Images).Length > 0);
    }

    [Fact]
    public void Refuses_huge_and_missing_files()
    {
        Assert.Contains("too large", Assert.Throws<ThemeImageException>(() =>
            _images.Import(Source("huge.png", ImageInfoTests.Png(20_000, 100)), ThemeImageKind.Background)).Message);
        Assert.Contains("too large", Assert.Throws<ThemeImageException>(() =>
            _images.Import(Source("many-pixels.png", ImageInfoTests.Png(9000, 9000)), ThemeImageKind.Background)).Message);
        Assert.Contains("can't be found", Assert.Throws<ThemeImageException>(() =>
            _images.Import(Path.Combine(_sourceFolder, "nope.png"), ThemeImageKind.Background)).Message);
        Assert.Contains("empty", Assert.Throws<ThemeImageException>(() =>
            _images.Import(Source("empty.png", []), ThemeImageKind.Background)).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData(@"..\settings.json")]
    [InlineData("background-0123456789abcdef.exe")]
    [InlineData("background-0123456789ABCDEF.png")]
    [InlineData("other-0123456789abcdef.png")]
    public void Only_its_own_file_names_resolve(string? name)
    {
        Assert.False(ThemeImages.IsValidName(name));
        Assert.Null(_images.Resolve(name));
    }

    [Fact]
    public void RemoveUnused_keeps_images_any_theme_uses()
    {
        var kept = _images.Import(Source("a.png", ImageInfoTests.Png(10, 10)), ThemeImageKind.Background);
        var dropped = _images.Import(Source("b.png", ImageInfoTests.Png(20, 20)), ThemeImageKind.Background);
        File.WriteAllText(Path.Combine(_temp.Paths.Images, "readme.txt"), "not ours");

        var removed = _images.RemoveUnused([Theme.Default with { BackgroundImagePath = kept }]);

        Assert.Equal(1, removed);
        Assert.NotNull(_images.Resolve(kept));
        Assert.Null(_images.Resolve(dropped));
        Assert.True(File.Exists(Path.Combine(_temp.Paths.Images, "readme.txt")));
    }
}

public sealed class ThemeLibraryTests : IDisposable
{
    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void There_are_three_valid_built_in_themes()
    {
        Assert.Equal(3, BuiltInThemes.All.Count);
        Assert.Equal(BuiltInThemes.All.Count, BuiltInThemes.All.Select(t => t.Name).Distinct().Count());
        foreach (var theme in BuiltInThemes.All) Assert.Equal(theme, ThemeLibrary.Clean(theme));
        Assert.True(BuiltInThemes.IsBuiltIn(" midnight "));
    }

    [Fact]
    public void Clean_brings_values_into_range()
    {
        var messy = Theme.Default with
        {
            Name = "  My\tCool   Theme  ",
            AccentColor = "red",
            BarColor = "#abc",
            Opacity = 0.1,
            BarHeight = 500,
            BackgroundFit = (ImageFit)42,
            FontFamily = "Arial, Comic Sans MS",
            BackgroundImagePath = @"C:\secret.png",
        };

        var clean = ThemeLibrary.Clean(messy);

        Assert.Equal("My Cool Theme", clean.Name);
        Assert.Equal(Theme.DefaultAccent, clean.AccentColor);
        Assert.Equal("#AABBCC", clean.BarColor);
        Assert.Equal(Theme.MinOpacity, clean.Opacity);
        Assert.Equal(Theme.MaxBarHeight, clean.BarHeight);
        Assert.Equal(ImageFit.Fill, clean.BackgroundFit);
        Assert.Equal(Theme.DefaultFont, clean.FontFamily);
        Assert.Null(clean.BackgroundImagePath);
    }

    [Fact]
    public void Missing_numbers_get_defaults_not_minimums()
    {
        var clean = ThemeLibrary.Clean(Theme.Default with { Opacity = 0, BarHeight = 0, Name = "" });
        Assert.Equal(1.0, clean.Opacity);
        Assert.Equal(Theme.DefaultBarHeight, clean.BarHeight);
        Assert.Equal("Custom", clean.Name);
    }

    [Theory]
    [InlineData("", "Give the theme a name.")]
    [InlineData("   ", "Give the theme a name.")]
    [InlineData("default", "built-in")]
    [InlineData("Sunrise", "built-in")]
    public void Name_problems(string name, string expected) => Assert.Contains(expected, ThemeLibrary.NameProblem(name));

    [Fact]
    public void Long_names_are_cut_short()
    {
        Assert.Null(ThemeLibrary.NameProblem(new string('a', 100)));
        Assert.Equal(ThemeLibrary.MaxNameLength, ThemeLibrary.NormalizeName(new string('a', 100)).Length);
    }

    [Fact]
    public void SaveAs_adds_or_replaces_and_becomes_current()
    {
        var settings = ThemeSettings.Default with { Current = BuiltInThemes.Midnight with { BarHeight = 60 } };
        Assert.True(settings.HasChanges);

        settings = ThemeLibrary.SaveAs(settings, "Night owl");
        Assert.Single(settings.Saved);
        Assert.Equal("Night owl", settings.Current.Name);
        Assert.Equal(60, settings.Saved[0].BarHeight);
        Assert.False(settings.HasChanges);

        Assert.True(ThemeLibrary.WouldReplace(settings, "NIGHT OWL"));
        settings = ThemeLibrary.SaveAs(settings with { Current = settings.Current with { BarHeight = 40 } }, "night owl");
        Assert.Single(settings.Saved);
        Assert.Equal(40, settings.Saved[0].BarHeight);
    }

    [Fact]
    public void SaveAs_refuses_built_in_names() =>
        Assert.Throws<ArgumentException>(() => ThemeLibrary.SaveAs(ThemeSettings.Default, "Midnight"));

    [Fact]
    public void Apply_and_delete()
    {
        var settings = ThemeLibrary.SaveAs(ThemeSettings.Default, "Mine");
        settings = ThemeLibrary.Apply(settings, BuiltInThemes.Sunrise);
        Assert.Equal(BuiltInThemes.Sunrise, settings.Current);
        Assert.False(settings.HasChanges);
        Assert.Equal(4, settings.All.Count());

        settings = ThemeLibrary.Delete(settings, "mine");
        Assert.Empty(settings.Saved);
        Assert.Equal(BuiltInThemes.Sunrise, settings.Current);
    }

    [Fact]
    public void Deleting_the_theme_in_use_keeps_the_look()
    {
        var settings = ThemeLibrary.SaveAs(ThemeSettings.Default with { Current = Theme.Default with { AccentColor = "#FF0000" } }, "Red");
        settings = ThemeLibrary.Delete(settings, "Red");

        Assert.Equal("#FF0000", settings.Current.AccentColor);
        Assert.Null(settings.Source);
        Assert.True(settings.HasChanges);
    }

    [Fact]
    public void A_custom_theme_survives_restart()
    {
        var store = new ThemeStore(new JsonFileStore(_temp.Paths));
        var custom = Theme.Default with
        {
            Name = "Mine",
            BackgroundImagePath = "background-0123456789abcdef.png",
            BackgroundFit = ImageFit.Center,
            BarColor = "#102030",
            AccentColor = "#FF8800",
            Opacity = 0.75,
            BarHeight = 56,
            FontFamily = "Bahnschrift",
        };
        store.Save(ThemeLibrary.SaveAs(ThemeSettings.Default with { Current = custom }, "Mine"));

        var loaded = new ThemeStore(new JsonFileStore(_temp.Paths)).Load();

        Assert.Equal(custom, loaded.Current);
        Assert.Equal(custom, Assert.Single(loaded.Saved));
    }

    [Fact]
    public void Loading_drops_duplicates_and_built_in_names()
    {
        var store = new JsonFileStore(_temp.Paths);
        store.Save(ThemeSettings.FileName, new ThemeSettings(Theme.Default, [
            Theme.Default with { Name = "Mine" },
            Theme.Default with { Name = "MINE", BarHeight = 60 },
            Theme.Default with { Name = "Midnight" },
        ]));

        var loaded = new ThemeStore(store).Load();

        Assert.Equal("Mine", Assert.Single(loaded.Saved).Name);
    }

    [Fact]
    public void Missing_or_partial_file_gives_defaults()
    {
        var store = new JsonFileStore(_temp.Paths);
        Assert.Equal(Theme.Default, new ThemeStore(store).Load().Current);

        Directory.CreateDirectory(_temp.Paths.Root);
        File.WriteAllText(_temp.Paths.File(ThemeSettings.FileName), """{ "Current": { "Name": "Old", "AccentColor": "#123456" } }""");
        var loaded = new ThemeStore(store).Load();

        Assert.Equal("Old", loaded.Current.Name);
        Assert.Equal("#123456", loaded.Current.AccentColor);
        Assert.Equal(1.0, loaded.Current.Opacity);
        Assert.Equal(Theme.DefaultBarHeight, loaded.Current.BarHeight);
        Assert.Equal(Theme.DefaultFont, loaded.Current.FontFamily);
        Assert.Empty(loaded.Saved);
    }
}
