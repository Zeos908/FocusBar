using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Appearance;

/// <summary>The three themes that come with FocusBar. They can be applied and changed, but not deleted or overwritten.</summary>
public static class BuiltInThemes
{
    /// <summary>Follows Windows' light or dark mode, with a blue accent.</summary>
    public static Theme Default => Theme.Default;

    /// <summary>Deep purple, a little see-through.</summary>
    public static Theme Midnight => Theme.Default with
    {
        Name = "Midnight",
        BarColor = "#17132E",
        AccentColor = "#A78BFA",
        Opacity = 0.9,
    };

    /// <summary>Warm and light, slightly shorter.</summary>
    public static Theme Sunrise => Theme.Default with
    {
        Name = "Sunrise",
        BarColor = "#FFF1E0",
        AccentColor = "#EA580C",
        BarHeight = 44,
        FontFamily = "Segoe UI",
    };

    public static IReadOnlyList<Theme> All { get; } = [Default, Midnight, Sunrise];

    public static bool IsBuiltIn(string name) => All.Any(t => t.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What's saved in <c>themes.json</c>: the theme in use right now (with any changes the student made) and their
/// own named themes.
/// </summary>
public sealed record ThemeSettings(Theme Current, IReadOnlyList<Theme> Saved)
{
    public const string FileName = "themes.json";
    public const int MaxSaved = 30;

    public static ThemeSettings Default => new(Theme.Default, []);

    /// <summary>Built-in themes first, then the student's.</summary>
    [JsonIgnore]
    public IEnumerable<Theme> All => BuiltInThemes.All.Concat(Saved);

    /// <summary>Every theme whose images must be kept (for <see cref="ThemeImages.RemoveUnused"/>).</summary>
    [JsonIgnore]
    public IEnumerable<Theme> InUse => Saved.Append(Current);

    /// <summary>The library theme named like the current one, if there is one.</summary>
    [JsonIgnore]
    public Theme? Source => All.FirstOrDefault(t => t.Name.Equals(Current.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the current theme has been changed since it was applied or saved.</summary>
    [JsonIgnore]
    public bool HasChanges => Source != Current;
}

/// <summary>Cleans up themes and does the save / apply / delete operations, without touching any files.</summary>
public static partial class ThemeLibrary
{
    public const int MaxNameLength = 40;
    public const int MaxFontNameLength = 64;

    /// <summary>
    /// Brings a theme (possibly from an old or hand-edited file) within the allowed ranges. Missing numbers get
    /// their defaults rather than the minimum.
    /// </summary>
    public static Theme Clean(Theme theme)
    {
        var opacity = theme.Opacity is > 0 and <= 1 ? Math.Round(Math.Max(theme.Opacity, Theme.MinOpacity), 2) : 1.0;
        var height = theme.BarHeight > 0 ? Math.Clamp(theme.BarHeight, Theme.MinBarHeight, Theme.MaxBarHeight) : Theme.DefaultBarHeight;

        return theme with
        {
            Name = NormalizeName(theme.Name) is { Length: > 0 } name ? name : "Custom",
            BackgroundImagePath = ThemeImages.IsValidName(theme.BackgroundImagePath) ? theme.BackgroundImagePath : null,
            StartIconPath = ThemeImages.IsValidName(theme.StartIconPath) ? theme.StartIconPath : null,
            FocusScreenImagePath = ThemeImages.IsValidName(theme.FocusScreenImagePath) ? theme.FocusScreenImagePath : null,
            AccentColor = ColorMath.Normalize(theme.AccentColor) ?? Theme.DefaultAccent,
            BarColor = ColorMath.Normalize(theme.BarColor),
            Opacity = opacity,
            BarHeight = height,
            BackgroundFit = Enum.IsDefined(theme.BackgroundFit) ? theme.BackgroundFit : ImageFit.Fill,
            FontFamily = IsValidFontName(theme.FontFamily) ? theme.FontFamily.Trim() : Theme.DefaultFont,
        };
    }

    /// <summary>Trims and collapses spaces; cuts to <see cref="MaxNameLength"/>.</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var clean = Spaces().Replace(new string(name.Select(c => char.IsControl(c) ? ' ' : c).ToArray()), " ").Trim();
        return clean.Length > MaxNameLength ? clean[..MaxNameLength].TrimEnd() : clean;
    }

    /// <summary>A font family name with nothing odd in it. (Whether it's installed is checked by the app.)</summary>
    public static bool IsValidFontName(string? font) =>
        !string.IsNullOrWhiteSpace(font) && font.Trim().Length <= MaxFontNameLength && !font.Any(c => char.IsControl(c) || c == ',');

    /// <summary>Why <paramref name="name"/> can't be used for a new saved theme, or null if it can.</summary>
    public static string? NameProblem(string? name)
    {
        var clean = NormalizeName(name);
        if (clean.Length == 0) return "Give the theme a name.";
        if (BuiltInThemes.IsBuiltIn(clean)) return $"\"{clean}\" is a built-in theme. Pick another name.";
        return null;
    }

    /// <summary>True if saving as <paramref name="name"/> would replace one of the student's themes.</summary>
    public static bool WouldReplace(ThemeSettings settings, string name) =>
        settings.Saved.Any(t => t.Name.Equals(NormalizeName(name), StringComparison.OrdinalIgnoreCase));

    /// <summary>Saves the current theme under <paramref name="name"/> (replacing a saved theme with that name).</summary>
    public static ThemeSettings SaveAs(ThemeSettings settings, string name)
    {
        if (NameProblem(name) is { } problem) throw new ArgumentException(problem, nameof(name));
        var clean = NormalizeName(name);
        var theme = Clean(settings.Current with { Name = clean });

        var saved = settings.Saved.Where(t => !t.Name.Equals(clean, StringComparison.OrdinalIgnoreCase)).ToList();
        if (saved.Count >= ThemeSettings.MaxSaved) throw new ArgumentException($"You can save up to {ThemeSettings.MaxSaved} themes. Delete one first.", nameof(name));
        saved.Add(theme);
        return new ThemeSettings(theme, [.. saved.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)]);
    }

    /// <summary>Makes <paramref name="theme"/> the current one.</summary>
    public static ThemeSettings Apply(ThemeSettings settings, Theme theme) => settings with { Current = Clean(theme) };

    /// <summary>Deletes one of the student's themes. Built-in themes can't be deleted. The current look stays as it is.</summary>
    public static ThemeSettings Delete(ThemeSettings settings, string name) =>
        settings with { Saved = [.. settings.Saved.Where(t => !t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))] };

    /// <summary>Cleans everything loaded from a file: bad values fixed, duplicate names and built-in names dropped.</summary>
    public static ThemeSettings Clean(ThemeSettings settings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var saved = (settings.Saved ?? [])
            .Where(t => t is not null)
            .Select(Clean)
            .Where(t => !BuiltInThemes.IsBuiltIn(t.Name) && seen.Add(t.Name))
            .Take(ThemeSettings.MaxSaved)
            .ToList();
        return new ThemeSettings(Clean(settings.Current ?? Theme.Default), saved);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>Loads and saves <see cref="ThemeSettings"/> in <c>themes.json</c>.</summary>
public sealed class ThemeStore
{
    private readonly JsonFileStore _store;

    public ThemeStore(JsonFileStore store)
    {
        _store = store;
    }

    public ThemeSettings Load() => ThemeLibrary.Clean(_store.Load<ThemeSettings?>(ThemeSettings.FileName, null) ?? ThemeSettings.Default);

    public void Save(ThemeSettings settings) => _store.Save(ThemeSettings.FileName, settings);
}
