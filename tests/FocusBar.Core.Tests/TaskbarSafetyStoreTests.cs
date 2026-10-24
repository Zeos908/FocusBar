using FocusBar.Core.Safety;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class TaskbarSafetyStoreTests : IDisposable
{
    private static readonly DateTimeOffset Started = new(2026, 10, 24, 8, 30, 0, 123, TimeSpan.FromHours(-4));

    private readonly TempAppData _temp = new();
    private readonly TaskbarSafetyStore _store;

    public TaskbarSafetyStoreTests()
    {
        _store = new TaskbarSafetyStore(new JsonFileStore(_temp.Paths));
    }

    public void Dispose() => _temp.Dispose();

    private static TaskbarSafetyState State(int pid = 1234, int originalState = 3) =>
        new(originalState, pid, Started, Started.AddSeconds(2));

    [Fact]
    public void No_file_means_nothing_to_recover()
    {
        Assert.Null(_store.Load());
        Assert.False(TaskbarSafetyStore.NeedsRecovery(_store.Load(), (_, _) => false));
    }

    [Fact]
    public void Round_trips_state_including_precise_start_time()
    {
        _store.Save(State());
        var loaded = _store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.OriginalAppBarState);
        Assert.True(TaskbarSafetyStore.IsOwnedBy(loaded, 1234, Started));
    }

    [Fact]
    public void Leftover_state_from_a_dead_process_needs_recovery()
    {
        _store.Save(State());
        Assert.True(TaskbarSafetyStore.NeedsRecovery(_store.Load(), (_, _) => false));
    }

    [Fact]
    public void State_owned_by_a_running_process_does_not_need_recovery()
    {
        _store.Save(State());
        Assert.False(TaskbarSafetyStore.NeedsRecovery(_store.Load(), (pid, start) => pid == 1234 && start == Started));
    }

    [Fact]
    public void Reused_process_id_is_not_mistaken_for_the_owner()
    {
        var state = State(pid: 1234);
        Assert.False(TaskbarSafetyStore.IsOwnedBy(state, 1234, Started.AddMinutes(5)));
        Assert.False(TaskbarSafetyStore.IsOwnedBy(state, 999, Started));
        Assert.False(TaskbarSafetyStore.IsOwnedBy(null, 1234, Started));
    }

    [Fact]
    public void Clear_removes_the_file()
    {
        _store.Save(State());
        _store.Clear();
        Assert.Null(_store.Load());
    }

    [Fact]
    public void Corrupt_state_file_is_treated_as_missing()
    {
        Directory.CreateDirectory(_temp.Paths.Root);
        File.WriteAllText(_temp.Paths.File(TaskbarSafetyStore.FileName), "garbage");
        Assert.Null(_store.Load());
    }
}
