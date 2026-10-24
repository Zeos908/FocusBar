using System.Globalization;
using System.Text;

namespace FocusBar.Core.Focus;

/// <summary>
/// Edits the Windows hosts file to block websites during Focus Mode. FocusBar's lines always sit between
/// <c># FocusBar BEGIN</c> and <c># FocusBar END</c>, and nothing outside those markers is ever changed.
/// Pure text in, text out, so it can be tested; the elevated FocusBar.Blocker helper does the file writing.
/// This file has no dependencies beyond .NET itself, because FocusBar.Blocker compiles it in directly.
/// </summary>
public static class HostsFile
{
    public const string BeginMarker = "# FocusBar BEGIN (added by FocusBar Focus Mode; removed when Focus Mode ends)";
    public const string EndMarker = "# FocusBar END";
    private const string BeginPrefix = "# FocusBar BEGIN";
    private const int MaxDomainLength = 253;

    /// <summary>Where Windows keeps the hosts file.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    /// <summary>
    /// Returns <paramref name="hostsText"/> with FocusBar's block replaced by one blocking <paramref name="domains"/>
    /// (and their "www." versions). Invalid domains are left out. With no valid domains, the block is just removed.
    /// </summary>
    public static string Apply(string hostsText, IEnumerable<string> domains)
    {
        var newline = DetectNewline(hostsText);
        var kept = Remove(hostsText).TrimEnd('\r', '\n');
        var valid = domains.Select(NormalizeDomain).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        if (valid.Count == 0) return kept.Length == 0 ? "" : kept + newline;

        var block = new StringBuilder();
        block.Append(BeginMarker).Append(newline);
        foreach (var domain in valid)
        {
            foreach (var name in new[] { domain, "www." + domain }.Distinct())
            {
                block.Append("0.0.0.0 ").Append(name).Append(newline);
                block.Append(":: ").Append(name).Append(newline);
            }
        }
        block.Append(EndMarker).Append(newline);

        return kept.Length == 0 ? block.ToString() : kept + newline + newline + block;
    }

    /// <summary>
    /// Returns <paramref name="hostsText"/> without FocusBar's block. Every other line is kept exactly.
    /// If the end marker is missing (a damaged file), only lines that look like FocusBar's own entries are removed.
    /// </summary>
    public static string Remove(string hostsText)
    {
        var newline = DetectNewline(hostsText);
        var lines = SplitLines(hostsText);
        var output = new List<string>(lines.Count);
        var hadTrailingNewline = hostsText.EndsWith('\n');

        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith(BeginPrefix, StringComparison.Ordinal))
            {
                output.Add(lines[i]);
                continue;
            }

            var end = lines.FindIndex(i + 1, l => l.Trim() == EndMarker);
            if (end >= 0)
            {
                i = end;
            }
            else
            {
                // No end marker: skip only lines that are clearly ours.
                var j = i + 1;
                while (j < lines.Count && IsOwnEntry(lines[j])) j++;
                i = j - 1;
            }

            // Drop the blank line FocusBar put before its block.
            if (output.Count > 0 && output[^1].Length == 0) output.RemoveAt(output.Count - 1);
        }

        var result = string.Join(newline, output);
        return hadTrailingNewline && result.Length > 0 ? result + newline : result;
    }

    public static bool HasBlock(string hostsText) =>
        SplitLines(hostsText).Any(l => l.StartsWith(BeginPrefix, StringComparison.Ordinal));

    /// <summary>The sites currently blocked by FocusBar's block (without the "www." copies).</summary>
    public static IReadOnlySet<string> BlockedDomains(string hostsText)
    {
        var domains = new HashSet<string>(StringComparer.Ordinal);
        var inside = false;
        foreach (var line in SplitLines(hostsText))
        {
            if (line.StartsWith(BeginPrefix, StringComparison.Ordinal)) { inside = true; continue; }
            if (line.Trim() == EndMarker) { inside = false; continue; }
            if (!inside || !IsOwnEntry(line)) continue;

            var name = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            domains.Add(name.StartsWith("www.", StringComparison.Ordinal) ? name[4..] : name);
        }
        return domains;
    }

    /// <summary>
    /// Turns what a student might type ("https://www.YouTube.com/watch?v=1", "youtube.com:443") into a plain
    /// domain ("youtube.com"), or null if it isn't a real domain name. International names become punycode.
    /// </summary>
    public static string? NormalizeDomain(string? input)
    {
        var text = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text)) return null;

        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) text = text[(scheme + 3)..];
        var cut = text.IndexOfAny(['/', '?', '#', ':', '\\']);
        if (cut >= 0) text = text[..cut];
        text = text.Trim('.');
        if (text.StartsWith("www.", StringComparison.Ordinal)) text = text[4..];

        try
        {
            text = new IdnMapping().GetAscii(text);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (text.Length is 0 or > MaxDomainLength || !text.Contains('.')) return null;
        var labels = text.Split('.');
        if (labels.Any(label => label.Length is 0 or > 63 || label.StartsWith('-') || label.EndsWith('-') ||
                                !label.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
        {
            return null;
        }

        // A bare IP address isn't a website name the hosts file can block.
        if (labels.All(label => label.All(char.IsAsciiDigit))) return null;
        return text;
    }

    private static bool IsOwnEntry(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0] is "0.0.0.0" or "::" && NormalizeDomain(parts[1]) is not null;
    }

    private static string DetectNewline(string text) => text.Contains("\r\n", StringComparison.Ordinal) || !text.Contains('\n') ? "\r\n" : "\n";

    private static List<string> SplitLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }
}
