using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Focus;

namespace FocusBar.App.Focus;

internal enum WebsiteBlockResult { Done, Declined, Failed }

/// <summary>
/// Blocks and unblocks websites by running FocusBar.Blocker.exe with administrator rights. Windows shows its
/// permission prompt (UAC) each time; FocusBar itself never runs as administrator and never edits the hosts file
/// directly. Reading the hosts file needs no special rights, so FocusBar checks it first and skips the prompt
/// when nothing needs to change.
/// </summary>
internal static class WebsiteBlocker
{
    private const int ErrorCancelled = 1223; // the student chose "No" on the permission prompt

    public static string HelperPath => Path.Combine(AppContext.BaseDirectory, "FocusBar.Blocker.exe");

    /// <summary>The sites FocusBar's block in the hosts file currently covers (empty if none or unreadable).</summary>
    public static IReadOnlySet<string> CurrentlyBlocked()
    {
        try
        {
            return File.Exists(HostsFile.DefaultPath) ? HostsFile.BlockedDomains(File.ReadAllText(HostsFile.DefaultPath)) : new HashSet<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new HashSet<string>();
        }
    }

    public static bool HasBlock()
    {
        try
        {
            return File.Exists(HostsFile.DefaultPath) && HostsFile.HasBlock(File.ReadAllText(HostsFile.DefaultPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Blocks exactly <paramref name="domains"/> (asks for permission only if that's a change).</summary>
    public static Task<WebsiteBlockResult> ApplyAsync(IReadOnlyCollection<string> domains, ErrorLog log)
    {
        if (domains.Count == 0) return ClearAsync(log);
        if (CurrentlyBlocked().SetEquals(domains)) return Task.FromResult(WebsiteBlockResult.Done);
        return RunAsync(["apply", .. domains], log);
    }

    /// <summary>Removes FocusBar's block (asks for permission only if there is one).</summary>
    public static Task<WebsiteBlockResult> ClearAsync(ErrorLog log) =>
        HasBlock() ? RunAsync(["clear"], log) : Task.FromResult(WebsiteBlockResult.Done);

    /// <summary>
    /// For exit paths that can't wait on a task: removes the block, waiting up to a minute for the student to answer
    /// the permission prompt.
    /// </summary>
    public static WebsiteBlockResult Clear(ErrorLog log) =>
        HasBlock() ? Run(["clear"], log, TimeSpan.FromMinutes(1)) : WebsiteBlockResult.Done;

    private static Task<WebsiteBlockResult> RunAsync(string[] args, ErrorLog log) =>
        Task.Run(() => Run(args, log, TimeSpan.FromMinutes(5)));

    private static WebsiteBlockResult Run(string[] args, ErrorLog log, TimeSpan wait)
    {
        if (!File.Exists(HelperPath))
        {
            log.Warning($"Can't block websites: {HelperPath} is missing.");
            return WebsiteBlockResult.Failed;
        }

        try
        {
            var info = new ProcessStartInfo(HelperPath)
            {
                UseShellExecute = true,
                Verb = "runas", // ask Windows for administrator rights (shows the permission prompt)
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (var arg in args) info.ArgumentList.Add(arg);

            using var process = Process.Start(info);
            if (process is null) return WebsiteBlockResult.Failed;
            if (!process.WaitForExit(wait))
            {
                log.Warning("The website blocker didn't finish in time.");
                return WebsiteBlockResult.Failed;
            }

            if (process.ExitCode == 0) return WebsiteBlockResult.Done;
            log.Warning($"The website blocker failed (code {process.ExitCode}); see %ProgramData%\\FocusBar\\blocker.log.");
            return WebsiteBlockResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            log.Info("The student declined the permission prompt for website blocking.");
            return WebsiteBlockResult.Declined;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            log.Write(ex, "Could not start the website blocker.");
            return WebsiteBlockResult.Failed;
        }
    }
}
