namespace FocusBar.Core.Updates;

public enum InstallAction
{
    /// <summary>No FocusBar installed yet: copy this one in.</summary>
    Install,

    /// <summary>An older (or the same) version is installed: replace it with this one.</summary>
    Update,

    /// <summary>The installed one is newer than this download: offer to start that instead.</summary>
    InstalledIsNewer,
}

/// <summary>
/// The rules for "extract the zip, run FocusBar.exe, and it updates the FocusBar you already have": FocusBar lives in
/// one fixed folder, and a copy started from anywhere else (a freshly extracted zip, marked by
/// <see cref="MarkerFileName"/>) offers to install or update that folder rather than becoming a second FocusBar.
/// </summary>
public static class PortableInstall
{
    /// <summary>Only the zip has this file; the installer and the installed copy don't.</summary>
    public const string MarkerFileName = "FocusBar.portable";

    /// <summary><c>%LocalAppData%\Programs\FocusBar</c>: the same place the installer uses for "install just for me".</summary>
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "FocusBar");

    public static InstallAction Decide(Version thisVersion, Version? installedVersion) =>
        installedVersion is null ? InstallAction.Install
        : Normalize(installedVersion) > Normalize(thisVersion) ? InstallAction.InstalledIsNewer
        : InstallAction.Update;

    /// <summary>True if two folder paths are the same folder (Windows paths ignore case and a trailing slash).</summary>
    public static bool SameFolder(string a, string b) =>
        string.Equals(Trim(a), Trim(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The files to copy from the extracted folder, as paths relative to it: everything except the marker, so the
    /// installed copy never thinks it's a download itself.
    /// </summary>
    public static IReadOnlyList<string> FilesToCopy(IEnumerable<string> relativePaths) =>
        [.. relativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Where(p => !Path.GetFileName(p.Replace('\\', '/')).Equals(MarkerFileName, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>"0.9.1" and "0.9.1.0" are the same version.</summary>
    private static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    private static string Trim(string path) => path.Trim().TrimEnd('\\', '/');
}
