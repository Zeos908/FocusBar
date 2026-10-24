using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly TempAppData _temp = new();
    private readonly JsonFileStore _store;

    public JsonFileStoreTests()
    {
        _store = new JsonFileStore(_temp.Paths, new ErrorLog(_temp.Paths));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Missing_file_returns_fallback()
    {
        Assert.Equal(Theme.Default, _store.Load("theme.json", Theme.Default));
    }

    [Fact]
    public void Round_trips_records_with_enums_and_lists()
    {
        var settings = FocusSettings.Default with { Strictness = Strictness.Strict, BlockedApps = ["Discord", "Steam"] };

        _store.Save("focus.json", settings);
        var loaded = _store.Load("focus.json", FocusSettings.Default);

        Assert.Equal(Strictness.Strict, loaded.Strictness);
        Assert.Equal(["Discord", "Steam"], loaded.BlockedApps);
        Assert.Equal(settings.StartBeforeDeadline, loaded.StartBeforeDeadline);
    }

    [Fact]
    public void Enums_are_written_as_readable_names()
    {
        _store.Save("focus.json", FocusSettings.Default);
        Assert.Contains("\"Soft\"", File.ReadAllText(_temp.Paths.File("focus.json")));
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        _store.Save("theme.json", Theme.Default);
        Assert.False(File.Exists(_temp.Paths.File("theme.json.tmp")));
    }

    [Fact]
    public void Save_overwrites_existing_file()
    {
        _store.Save("theme.json", Theme.Default);
        _store.Save("theme.json", Theme.Default with { Name = "Ocean" });
        Assert.Equal("Ocean", _store.Load("theme.json", Theme.Default).Name);
    }

    [Fact]
    public void Corrupt_file_is_set_aside_and_fallback_returned()
    {
        Directory.CreateDirectory(_temp.Paths.Root);
        File.WriteAllText(_temp.Paths.File("theme.json"), "{ this is not json");

        var loaded = _store.Load("theme.json", Theme.Default);

        Assert.Equal(Theme.Default, loaded);
        Assert.False(File.Exists(_temp.Paths.File("theme.json")));
        Assert.Single(Directory.GetFiles(_temp.Paths.Root, "theme.json.corrupt-*"));
        Assert.Contains("Could not read theme.json", File.ReadAllText(new ErrorLog(_temp.Paths).CurrentLogFile));
    }

    [Fact]
    public void Delete_is_safe_when_file_is_missing()
    {
        _store.Delete("nothing-here.json");
        Assert.False(_store.Exists("nothing-here.json"));
    }
}
