namespace FocusBar.Core.Reminders;

/// <summary>
/// What the reminder system remembers between runs (saved to <c>reminders.json</c>), so a reminder is never
/// shown twice, even after a restart.
/// </summary>
public sealed class ReminderState
{
    /// <summary>Reminders already shown (or deliberately skipped). See <see cref="ReminderPlanner.Key"/>.</summary>
    public HashSet<string> Fired { get; set; } = [];

    /// <summary>When FocusBar first saw each deadline. Reminders that were due before then are skipped.</summary>
    public Dictionary<Guid, DateTimeOffset> FirstSeen { get; set; } = [];

    /// <summary>Snoozed deadlines and when to remind again.</summary>
    public Dictionary<Guid, DateTimeOffset> Snoozed { get; set; } = [];

    public void Snooze(Guid deadlineId, DateTimeOffset until) => Snoozed[deadlineId] = until;

    /// <summary>Forgets deadlines that no longer exist so the file doesn't grow forever.</summary>
    public bool Prune(IReadOnlySet<Guid> existing)
    {
        var before = Fired.Count + FirstSeen.Count + Snoozed.Count;
        Fired.RemoveWhere(key => !Guid.TryParseExact(key.Split('|')[0], "N", out var id) || !existing.Contains(id));
        foreach (var id in FirstSeen.Keys.Where(id => !existing.Contains(id)).ToList()) FirstSeen.Remove(id);
        foreach (var id in Snoozed.Keys.Where(id => !existing.Contains(id)).ToList()) Snoozed.Remove(id);
        return Fired.Count + FirstSeen.Count + Snoozed.Count != before;
    }
}
