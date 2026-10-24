using FocusBar.Core.Storage;

namespace FocusBar.Core.Pins;

/// <summary>Saves pinned apps to <c>pins.json</c>.</summary>
public sealed class PinStore
{
    public const string FileName = "pins.json";

    private readonly JsonFileStore _store;

    public PinStore(JsonFileStore store)
    {
        _store = store;
    }

    public PinnedAppList Load() => new(_store.Load(FileName, new PinFile([])).Apps);

    public void Save(PinnedAppList pins) => _store.Save(FileName, new PinFile([.. pins.Apps]));

    private sealed record PinFile(List<PinnedApp> Apps, int Version = 1);
}
