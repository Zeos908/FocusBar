using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

/// <summary>A throwaway app-data folder per test.</summary>
public sealed class TempAppData : IDisposable
{
    public TempAppData()
    {
        Paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), "FocusBarTests", Guid.NewGuid().ToString("N")));
    }

    public AppDataPaths Paths { get; }

    public void Dispose()
    {
        if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
    }
}
