using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Deadlines;

/// <summary>
/// The weekly class schedule, saved to <c>schedule.json</c>. Filled in by the school API sync (Phase 5);
/// until then it's empty and the bar doesn't show a class.
/// </summary>
public sealed class ScheduleStore
{
    public const string FileName = "schedule.json";

    private readonly JsonFileStore _store;

    public ScheduleStore(JsonFileStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ClassPeriod> Load() =>
        [.. _store.Load(FileName, new ScheduleFile([])).Classes
            .Where(c => !string.IsNullOrWhiteSpace(c.ClassName) && c.Start < c.End)];

    public void Save(IEnumerable<ClassPeriod> classes) => _store.Save(FileName, new ScheduleFile([.. classes]));

    private sealed record ScheduleFile(List<ClassPeriod> Classes, int Version = 1);
}
