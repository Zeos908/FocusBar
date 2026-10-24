using System.Diagnostics;
using System.Text;
using FocusBar.Core.Focus;

namespace FocusBar.Blocker;

/// <summary>
/// Usage:
/// <code>
/// FocusBar.Blocker.exe apply youtube.com reddit.com   (block these sites; replaces any earlier FocusBar block)
/// FocusBar.Blocker.exe clear                          (remove FocusBar's block)
/// </code>
/// Only FocusBar's own marked block is ever changed. The original hosts file is backed up (once) before the
/// first change, to <c>hosts.focusbar-backup</c> next to it. Every site name is checked again here, so this
/// program can only ever point real domain names at 0.0.0.0 (nowhere), never redirect anything elsewhere.
/// </summary>
internal static class Program
{
    private const int Ok = 0;
    private const int BadArguments = 2;
    private const int FileProblem = 3;
    private const int NotAllowed = 4;
    private const int MaxSites = 400;

    public static int Main(string[] args)
    {
        if (args.Length == 0) return BadArguments;
        var command = args[0].ToLowerInvariant();
        if (command is not ("apply" or "clear")) return BadArguments;

        var domains = args.Skip(1).Select(HostsFile.NormalizeDomain).OfType<string>().Distinct().Take(MaxSites).ToList();
        var path = HostsFile.DefaultPath;

        try
        {
            var (text, encoding) = Read(path);
            var updated = command == "apply" ? HostsFile.Apply(text, domains) : HostsFile.Remove(text);
            if (updated == text) return Ok;

            BackUpOnce(path);
            Write(path, updated, encoding);
            FlushDns();
            // How many, not which: the log stays useful without listing the student's sites.
            Log(command == "apply" ? $"apply: blocking {domains.Count} site(s)" : "clear: removed FocusBar's block");
            return Ok;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log($"{command}: not allowed: {ex.Message}");
            return NotAllowed;
        }
        catch (IOException ex)
        {
            Log($"{command}: file problem: {ex.Message}");
            return FileProblem;
        }
    }

    /// <summary>Keeps whatever encoding the file uses (a byte-order mark, or plain ASCII/UTF-8).</summary>
    private static (string Text, Encoding Encoding) Read(string path)
    {
        if (!File.Exists(path)) return ("", new UTF8Encoding(false));
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(hasBom);
        return (encoding.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), encoding);
    }

    private static void BackUpOnce(string path)
    {
        var backup = path + ".focusbar-backup";
        if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
    }

    /// <summary>
    /// Writes a temporary file, then swaps it in, so a crash mid-write can't leave a half-written hosts file.
    /// Retries briefly because antivirus software sometimes holds the hosts file open.
    /// </summary>
    private static void Write(string path, string text, Encoding encoding)
    {
        var temp = path + ".focusbar-tmp";
        var attributes = File.Exists(path) ? File.GetAttributes(path) : FileAttributes.Normal;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(temp, text, encoding);
                if (File.Exists(path))
                {
                    if (attributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                    File.Replace(temp, path, null);
                    File.SetAttributes(path, attributes);
                }
                else
                {
                    File.Move(temp, path);
                }
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200 * attempt);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Makes Windows forget cached website addresses so the change takes effect right away.</summary>
    private static void FlushDns()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
            process?.WaitForExit(10_000);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log($"flushdns: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FocusBar");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "blocker.log"), $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {message}{Environment.NewLine}");
        }
        catch
        {
            // Nowhere else to report it; FocusBar sees the exit code.
        }
    }
}
