using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Media;
using FocusBar.Core.Pins;
using ManagedShell.Common.Enums;
using ManagedShell.Common.Helpers;

namespace FocusBar.App.Services;

/// <summary>
/// Loads app icons (for shortcuts, programs, and Store apps) off the UI thread and caches them.
/// Uses ManagedShell's icon thread, which is set up for the Windows shell's COM requirements.
/// </summary>
internal static class IconLoader
{
    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<ImageSource?> LoadAsync(string path) =>
        Cache.GetOrAdd(path, p => Task.Factory.StartNew(
            () => Load(p),
            CancellationToken.None,
            TaskCreationOptions.None,
            IconHelper.IconScheduler));

    private static ImageSource? Load(string path)
    {
        try
        {
            if (path.StartsWith(PinnedApp.AppxPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return LoadStoreAppIcon(path[PinnedApp.AppxPrefix.Length..]);
            }

            return IconImageConverter.GetImageFromAssociatedIcon(path, IconSize.Large);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Store apps have no file to ask for an icon, so look them up in the shell's Apps folder.</summary>
    private static ImageSource? LoadStoreAppIcon(string appUserModelId)
    {
        if (SHParseDisplayName($@"shell:AppsFolder\{appUserModelId}", IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return IconImageConverter.GetImageFromAssociatedIcon(pidl, IconSize.Large);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);
}
