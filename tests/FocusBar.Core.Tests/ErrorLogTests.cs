using FocusBar.Core.Diagnostics;

namespace FocusBar.Core.Tests;

public sealed class ErrorLogTests : IDisposable
{
    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Writes_to_a_dated_file_in_the_logs_folder()
    {
        var log = new ErrorLog(_temp.Paths, () => new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

        log.Write(new InvalidOperationException("boom"), "Something failed");

        var file = Path.Combine(_temp.Paths.Logs, "2026-10-24.log");
        Assert.Equal(file, log.CurrentLogFile);
        var text = File.ReadAllText(file);
        Assert.Contains("[ERROR]", text);
        Assert.Contains("Something failed", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Appends_rather_than_overwrites()
    {
        var log = new ErrorLog(_temp.Paths);
        log.Info("first");
        log.Warning("second");

        var text = File.ReadAllText(log.CurrentLogFile);
        Assert.Contains("[INFO]", text);
        Assert.Contains("first", text);
        Assert.Contains("[WARN]", text);
        Assert.Contains("second", text);
    }

    [Fact]
    public void Never_throws_even_when_the_folder_cannot_be_created()
    {
        // A file where the logs folder should be makes Directory.CreateDirectory fail.
        Directory.CreateDirectory(_temp.Paths.Root);
        File.WriteAllText(_temp.Paths.Logs, "not a folder");

        var log = new ErrorLog(_temp.Paths);
        log.Write(new Exception("x"), "context");
    }
}
