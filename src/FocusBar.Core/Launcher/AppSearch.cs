namespace FocusBar.Core.Launcher;

/// <summary>
/// Filters and ranks apps as the user types in the Start launcher.
/// Best matches first: exact name, then name starts with the text, then a word starts with it,
/// then initials ("vsc" for Visual Studio Code), then the text appears anywhere.
/// </summary>
public static class AppSearch
{
    private static readonly char[] WordSeparators = [' ', '-', '_', '.', '(', ')', '[', ']', '&', '+', ','];

    public static IReadOnlyList<AppEntry> Filter(IEnumerable<AppEntry> apps, string? query)
    {
        var text = query?.Trim() ?? "";
        if (text.Length == 0)
        {
            return [.. apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        return [.. apps
            .Select(app => (app, rank: Rank(app.Name, text)))
            .Where(x => x.rank is not null)
            .OrderBy(x => x.rank)
            .ThenBy(x => x.app.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.app)];
    }

    /// <summary>Lower is better; null means no match.</summary>
    internal static int? Rank(string name, string query)
    {
        const StringComparison ignoreCase = StringComparison.CurrentCultureIgnoreCase;

        if (name.Equals(query, ignoreCase)) return 0;
        if (name.StartsWith(query, ignoreCase)) return 1;

        var words = name.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => word.StartsWith(query, ignoreCase))) return 2;

        if (query.Length >= 2 && !query.Contains(' '))
        {
            var initials = string.Concat(words.Select(word => word[0]));
            if (initials.StartsWith(query, ignoreCase)) return 3;
        }

        if (name.Contains(query, ignoreCase)) return 4;
        return null;
    }
}
