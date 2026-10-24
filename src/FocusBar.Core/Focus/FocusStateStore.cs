using FocusBar.Core.Storage;

namespace FocusBar.Core.Focus;

/// <summary>What Focus Mode remembers across restarts: a running timed session, and automatic sessions ended early.</summary>
public sealed class FocusState
{
    public ManualSession? Manual { get; set; }

    /// <summary>Keys from <see cref="FocusPlanner.DismissKey"/>.</summary>
    public HashSet<string> Dismissed { get; set; } = [];
}

/// <summary>Saves <see cref="FocusState"/> to <c>focus-state.json</c>.</summary>
public sealed class FocusStateStore
{
    public const string FileName = "focus-state.json";

    private readonly JsonFileStore _store;

    public FocusStateStore(JsonFileStore store)
    {
        _store = store;
    }

    public FocusState Load()
    {
        var state = _store.Load<FocusState?>(FileName, null) ?? new FocusState();
        state.Dismissed ??= [];
        return state;
    }

    public void Save(FocusState state) => _store.Save(FileName, state);
}
