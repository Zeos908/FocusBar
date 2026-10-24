using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Deadlines;

/// <summary>Saves deadlines to <c>deadlines.json</c>.</summary>
public sealed class DeadlineStore
{
    public const string FileName = "deadlines.json";

    private readonly JsonFileStore _store;

    public DeadlineStore(JsonFileStore store)
    {
        _store = store;
    }

    public DeadlineList Load() => new(_store.Load(FileName, new DeadlineFile([])).Deadlines);

    public void Save(DeadlineList deadlines) => _store.Save(FileName, new DeadlineFile([.. deadlines.All]));

    private sealed record DeadlineFile(List<Deadline> Deadlines, int Version = 1);
}
