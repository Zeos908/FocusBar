using FocusBar.Core.Models;

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

    /// <summary>Not complete yet, soonest (or most overdue) first. This is what the ticker shows.</summary>
    public IReadOnlyList<Deadline> Upcoming() =>
        [.. _items.Where(d => !d.IsComplete).OrderBy(d => d.DueAt).ThenBy(d => d.Title, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>Completed, most recent due date first.</summary>
    public IReadOnlyList<Deadline> Completed() =>
        [.. _items.Where(d => d.IsComplete).OrderByDescending(d => d.DueAt).ThenBy(d => d.Title, StringComparer.CurrentCultureIgnoreCase)];

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

    public bool SetComplete(Guid id, bool complete)
    {
        var index = _items.FindIndex(d => d.Id == id);
        if (index < 0 || _items[index].IsComplete == complete) return false;
        _items[index] = _items[index] with { IsComplete = complete };
        return true;
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
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
