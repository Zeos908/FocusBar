using System.Text;
using FocusBar.Core.Models;

namespace FocusBar.Core.Deadlines;

/// <summary>
/// Finds the same assignment coming from more than one place (say, typed in by hand and also synced from the
/// school website) so it shows once. Two deadlines are duplicates when their titles match (ignoring case,
/// punctuation, and spacing), their due times are within an hour, and they come from different sources.
/// The one kept is the manual entry if there is one, otherwise the school's, otherwise the calendar's.
/// Nothing is deleted: duplicates are only hidden, so if the kept one is deleted the other reappears.
/// </summary>
public static class DeadlineMerger
{
    public static readonly TimeSpan SameTimeWindow = TimeSpan.FromHours(1);

    /// <summary>"Ch. 7 Reading — Questions!" → "ch 7 reading questions".</summary>
    public static string NormalizeTitle(string title)
    {
        var builder = new StringBuilder(title.Length);
        var lastWasSpace = true;
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }
        return builder.ToString().TrimEnd();
    }

    public static bool AreDuplicates(Deadline a, Deadline b) =>
        a.Id != b.Id
        && !SameOrigin(a, b)
        && (a.DueAt - b.DueAt).Duration() <= SameTimeWindow
        && NormalizeTitle(a.Title) == NormalizeTitle(b.Title);

    /// <summary>
    /// Groups duplicates together. Each group is ordered with the one to show first.
    /// Deadlines with no duplicates are groups of one.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Deadline>> Group(IEnumerable<Deadline> deadlines)
    {
        var groups = new List<List<Deadline>>();

        // Only deadlines with the same normalized title can be duplicates, so compare within those buckets.
        foreach (var bucket in deadlines.GroupBy(d => NormalizeTitle(d.Title)))
        {
            var members = bucket.ToList();
            var parent = Enumerable.Range(0, members.Count).ToArray();
            int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);

            for (var i = 0; i < members.Count; i++)
            {
                for (var j = i + 1; j < members.Count; j++)
                {
                    if (AreDuplicates(members[i], members[j])) parent[Root(i)] = Root(j);
                }
            }

            groups.AddRange(members
                .Select((d, i) => (d, root: Root(i)))
                .GroupBy(x => x.root)
                .Select(g => g.Select(x => x.d).OrderBy(Preference).ThenBy(d => d.DueAt).ToList()));
        }

        return groups;
    }

    /// <summary>
    /// One deadline per group: the preferred one, marked complete if any copy is complete
    /// (finishing it anywhere means it's done).
    /// </summary>
    public static IReadOnlyList<Deadline> Deduplicate(IEnumerable<Deadline> deadlines) =>
        [.. Group(deadlines).Select(g => g[0] with { IsComplete = g.Any(d => d.IsComplete) })];

    private static int Preference(Deadline d) => d.Source switch
    {
        DeadlineSource.Manual => 0,
        DeadlineSource.SchoolApi => 1,
        _ => 2,
    };

    /// <summary>
    /// Two hand-typed entries are never merged (the student may really have two), and neither are two items
    /// from the same synced source (that source says they're different).
    /// </summary>
    private static bool SameOrigin(Deadline a, Deadline b)
    {
        if (a.Source != b.Source) return false;
        if (a.Source == DeadlineSource.Manual) return true;
        return SourceKeyOf(a) == SourceKeyOf(b);
    }

    /// <summary>Synced deadlines store "sourceKey:externalId" in <see cref="Deadline.ExternalId"/>.</summary>
    public static string? SourceKeyOf(Deadline d)
    {
        var id = d.ExternalId;
        if (id is null) return null;
        var colon = id.IndexOf(':');
        return colon < 0 ? id : id[..colon];
    }
}
