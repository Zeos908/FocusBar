using System.Text;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Diagnostics;

/// <summary>
/// Appends errors to <c>%AppData%\FocusBar\logs\yyyy-MM-dd.log</c>.
/// Logging must never be the thing that crashes the app, so every method swallows its own failures.
/// </summary>
public sealed class ErrorLog
{
    private static readonly object WriteLock = new();
    private readonly AppDataPaths _paths;
    private readonly Func<DateTimeOffset> _now;

    public ErrorLog(AppDataPaths paths, Func<DateTimeOffset>? now = null)
    {
        _paths = paths;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string CurrentLogFile => Path.Combine(_paths.Logs, $"{_now():yyyy-MM-dd}.log");

    public void Write(Exception exception, string context) =>
        Append("ERROR", $"{context}{Environment.NewLine}{exception}");

    public void Info(string message) => Append("INFO", message);

    public void Warning(string message) => Append("WARN", message);

    private void Append(string level, string message)
    {
        try
        {
            var line = new StringBuilder()
                .Append(_now().ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append(" [").Append(level).Append("] ")
                .Append("[pid ").Append(Environment.ProcessId).Append("] ")
                .AppendLine(message)
                .ToString();

            lock (WriteLock)
            {
                Directory.CreateDirectory(_paths.Logs);
                System.IO.File.AppendAllText(CurrentLogFile, line);
            }
        }
        catch
        {
            // Nowhere left to report this; never let logging throw.
        }
    }
}
