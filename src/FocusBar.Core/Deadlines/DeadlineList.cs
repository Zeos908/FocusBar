using FocusBar.Core.Models;
using FocusBar.Core.Sources;

namespace FocusBar.Core.Deadlines;

/// <summary>
/// All of the student's deadlines. Pure logic; <see cref="DeadlineStore"/> saves it.
/// </summary>
public sealed class DeadlineList
{
    public const int MaxTitleLength = 120;
    public const int MaxClassLength = 60;
    public const int MaxDescriptionLength = 2000;

    private readonly List<Deadline> _items;

    public DeadlineList(IEnumerable<Deadline>? items = null)
    {
        // Drop blank or duplicate entries a hand-edited file might contain.
        _items = [.. (items ?? [])
            .Where(d => d.Id != Guid.Empty && !string.IsNullOrWhiteSpace(d.Title))
            .GroupBy(d => d.Id)
            .Select(g => g.First())];
    }

    public IReadOnlyList<Deadline> All => _items;

    public Deadline? Find(Guid id) => _items.FirstOrDefault(d => d.Id == id);

    /// <summary>
    /// Every deadline once: duplicates of the same assignment from different sources are folded together
    /// (see <see cref="DeadlineMerger"/>).
    /// </summary>
    public IReadOnlyList<Deadline> Merged() => DeadlineMerger.Deduplicate(_items);

    /// <summary>Not complete yet, soonest (or most overdue) first. This is what the ticker shows.</summary>
    public IReadOnlyList<Deadline> Upcoming() =>
        [.. Merged().Where(d => !d.IsComplete).OrderBy(d => d.DueAt).ThenBy(d => d.Title, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>Completed, most recent due date first.</summary>
    public IReadOnlyList<Deadline> Completed() =>
        [.. Merged().Where(d => d.IsComplete).OrderByDescending(d => d.DueAt).ThenBy(d => d.Title, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>Adds a deadline the student typed in. Text is trimmed and blank optional fields become null.</summary>
    public Deadline AddManual(string title, string? className, string? description, DateTimeOffset dueAt)
    {
        var deadline = new Deadline(
            Id: Guid.NewGuid(),
            Title: CleanRequired(title, MaxTitleLength, nameof(title)),
            ClassName: CleanOptional(className, MaxClassLength),
            Description: CleanOptional(description, MaxDescriptionLength),
            DueAt: dueAt,
            Source: DeadlineSource.Manual,
            ExternalId: null,
            IsComplete: false);
        _items.Add(deadline);
        return deadline;
    }

    /// <summary>Changes the editable fields of an existing deadline. Returns false if it no longer exists.</summary>
    public bool Edit(Guid id, string title, string? className, string? description, DateTimeOffset dueAt)
    {
        var index = _items.FindIndex(d => d.Id == id);
        if (index < 0) return false;

        _items[index] = _items[index] with
        {
            Title = CleanRequired(title, MaxTitleLength, nameof(title)),
            ClassName = CleanOptional(className, MaxClassLength),
            Description = CleanOptional(description, MaxDescriptionLength),
            DueAt = dueAt,
        };
        return true;
    }

    /// <summary>
    /// Marks a deadline (and any duplicates of it from other sources) complete or not complete.
    /// Returns false if nothing changed.
    /// </summary>
    public bool SetComplete(Guid id, bool complete)
    {
        var target = Find(id);
        if (target is null) return false;

        var group = DeadlineMerger.Group(_items).FirstOrDefault(g => g.Any(d => d.Id == id)) ?? [target];
        var changed = false;
        foreach (var member in group)
        {
            var index = _items.FindIndex(d => d.Id == member.Id);
            if (_items[index].IsComplete == complete) continue;
            _items[index] = _items[index] with { IsComplete = complete };
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Brings in the latest deadlines from a synced source (school website or calendar):
    /// new ones are added, known ones (matched by the source's own ID) are updated, and ones in the
    /// source's date range that the source no longer lists are removed. Other sources are untouched.
    /// Completing a deadline in FocusBar sticks even if the source still lists it as to-do.
    /// </summary>
    public SyncChanges ApplySync(string sourceKey, DeadlineSource kind, IEnumerable<Deadline> fetched, DateRange covers)
    {
        if (kind == DeadlineSource.Manual) throw new ArgumentException("Manual deadlines aren't synced.", nameof(kind));

        var prefix = sourceKey + ":";
        var existing = _items
            .Select((d, index) => (d, index))
            .Where(x => x.d.Source == kind && x.d.ExternalId?.StartsWith(prefix, StringComparison.Ordinal) == true)
            .GroupBy(x => x.d.ExternalId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().index, StringComparer.Ordinal);

        int added = 0, updated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in fetched)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId) || string.IsNullOrWhiteSpace(item.Title)) continue;
            var key = prefix + item.ExternalId.Trim();
            if (!seen.Add(key)) continue; // the source listed it twice

            var title = Clip(item.Title.Trim(), MaxTitleLength);
            var className = CleanOptional(item.ClassName, MaxClassLength);
            var description = CleanOptional(item.Description, MaxDescriptionLength);

            if (existing.TryGetValue(key, out var index))
            {
                var current = _items[index];
                var next = current with
                {
                    Title = title,
                    ClassName = className,
                    Description = description,
                    DueAt = item.DueAt,
                    IsComplete = current.IsComplete || item.IsComplete,
                };
                if (next != current)
                {
                    _items[index] = next;
                    updated++;
                }
            }
            else
            {
                _items.Add(new Deadline(Guid.NewGuid(), title, className, description, item.DueAt, kind, key, item.IsComplete));
                added++;
            }
        }

        var removed = _items.RemoveAll(d =>
            d.Source == kind
            && d.ExternalId is { } id && id.StartsWith(prefix, StringComparison.Ordinal)
            && !seen.Contains(id)
            && covers.Contains(d.DueAt));

        return new SyncChanges(added, updated, removed);
    }

    /// <summary>Removes everything a source added, e.g. when the student disconnects it.</summary>
    public int RemoveSource(string sourceKey)
    {
        var prefix = sourceKey + ":";
        return _items.RemoveAll(d => d.Source != DeadlineSource.Manual && d.ExternalId?.StartsWith(prefix, StringComparison.Ordinal) == true);
    }

    public bool Remove(Guid id) => _items.RemoveAll(d => d.Id == id) > 0;

    /// <summary>Class names already used, most recently due first, for quick picking in the add form.</summary>
    public IReadOnlyList<string> RecentClassNames(int max) =>
        [.. _items
            .Where(d => !string.IsNullOrWhiteSpace(d.ClassName))
            .OrderByDescending(d => d.DueAt)
            .Select(d => d.ClassName!)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(max)];

    private static string CleanRequired(string value, int maxLength, string name)
    {
        var text = CleanOptional(value, maxLength);
        return text ?? throw new ArgumentException("A title is required.", name);
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return Clip(text, maxLength);
    }

    private static string Clip(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}

/// <summary>What a sync changed, for the log and the Settings status line.</summary>
public readonly record struct SyncChanges(int Added, int Updated, int Removed)
{
    public bool Any => Added + Updated + Removed > 0;
}
