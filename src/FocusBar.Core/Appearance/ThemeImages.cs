using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Appearance;

public enum ThemeImageKind { Background, StartIcon, FocusScreen }

/// <summary>A friendly reason an image can't be used.</summary>
public sealed class ThemeImageException(string message) : Exception(message);

/// <summary>
/// The student's images, copied into <c>%AppData%\FocusBar\images</c> so the originals can move or be deleted.
/// Files are named after their kind and contents (e.g. <c>background-1f2e3d4c5b6a7980.png</c>), so picking the same
/// image twice doesn't make a second copy, and themes store just that name.
/// </summary>
public sealed partial class ThemeImages
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
    public const int MaxSide = 10_000;
    public const long MaxPixels = 40_000_000;

    private readonly AppDataPaths _paths;

    public ThemeImages(AppDataPaths paths)
    {
        _paths = paths;
    }

    public string Folder => _paths.Images;

    /// <summary>
    /// Checks <paramref name="sourcePath"/> is a PNG, JPG, or GIF of a sensible size and copies it in.
    /// Returns the stored file name to put in the theme. Throws <see cref="ThemeImageException"/> with a message
    /// for the student when it can't be used.
    /// </summary>
    public string Import(string sourcePath, ThemeImageKind kind)
    {
        byte[] bytes;
        try
        {
            var file = new FileInfo(sourcePath);
            if (!file.Exists) throw new ThemeImageException("That file can't be found.");
            if (file.Length > MaxFileBytes) throw new ThemeImageException("That image is too big (over 25 MB). Try a smaller copy.");
            if (file.Length == 0) throw new ThemeImageException("That file is empty.");
            bytes = File.ReadAllBytes(sourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new ThemeImageException("FocusBar couldn't open that file.");
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var info = ImageInfo.TryRead(stream)
            ?? throw new ThemeImageException("That isn't a PNG, JPG, or GIF image.");
        if (info.Width > MaxSide || info.Height > MaxSide || (long)info.Width * info.Height > MaxPixels)
        {
            throw new ThemeImageException($"That image is too large ({info.Width:N0} × {info.Height:N0} pixels). Try one under 10,000 pixels on each side.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
        var name = $"{Prefix(kind)}-{hash}{info.Extension}";
        var destination = Path.Combine(Folder, name);

        try
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(destination))
            {
                var temp = destination + ".tmp";
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, destination, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ThemeImageException("FocusBar couldn't save a copy of that image.");
        }
        return name;
    }

    /// <summary>The full path of a stored image, or null if the name isn't one of ours or the file is gone.</summary>
    public string? Resolve(string? name)
    {
        if (!IsValidName(name)) return null;
        var path = Path.Combine(Folder, name!);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// True for names <see cref="Import"/> makes. Anything else (a full path, "..\", a hand-edited name) is ignored,
    /// so a theme file can only point at FocusBar's own image folder.
    /// </summary>
    public static bool IsValidName(string? name) => name is not null && StoredName().IsMatch(name);

    /// <summary>Deletes stored images no theme uses any more. Returns how many were removed.</summary>
    public int RemoveUnused(IEnumerable<Theme> themes)
    {
        if (!Directory.Exists(Folder)) return 0;
        var used = themes
            .SelectMany(t => new[] { t.BackgroundImagePath, t.StartIconPath, t.FocusScreenImagePath })
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(Folder))
        {
            var name = Path.GetFileName(path);
            if (!IsValidName(name) || used.Contains(name)) continue;
            try
            {
                File.Delete(path);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use or locked; try again next time.
            }
        }
        return removed;
    }

    private static string Prefix(ThemeImageKind kind) => kind switch
    {
        ThemeImageKind.Background => "background",
        ThemeImageKind.StartIcon => "start",
        _ => "focus",
    };

    [GeneratedRegex(@"^(background|start|focus)-[0-9a-f]{16}\.(png|jpg|gif)$")]
    private static partial Regex StoredName();
}
