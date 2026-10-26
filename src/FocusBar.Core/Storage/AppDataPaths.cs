namespace FocusBar.Core.Storage;

/// <summary>
/// Where FocusBar keeps its files. Everything lives under one folder on the user's computer
/// (<c>%AppData%\FocusBar</c> on Windows). Tests pass their own temporary root.
/// </summary>
public sealed class AppDataPaths
{
    public AppDataPaths(string root)
    {
        Root = root;
    }

    public static AppDataPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusBar"));

    public string Root { get; }
    public string Logs => Path.Combine(Root, "logs");
    public string Images => Path.Combine(Root, "images");

    /// <summary>FocusBar's copies of shortcuts brought over from the Windows taskbar's pins.</summary>
    public string PinShortcuts => Path.Combine(Root, "pins");

    public string File(string fileName) => Path.Combine(Root, fileName);
}
