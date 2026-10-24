using System.Text.Json;
using System.Text.Json.Serialization;
using FocusBar.Core.Diagnostics;

namespace FocusBar.Core.Storage;

/// <summary>
/// Reads and writes JSON files under <see cref="AppDataPaths.Root"/>.
/// Writes go to a temporary file first and are then swapped in, so a crash mid-write never
/// leaves a half-written file. A file that can't be parsed is set aside (not deleted) and
/// the caller gets the fallback value instead of an exception.
/// </summary>
public sealed class JsonFileStore
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AppDataPaths _paths;
    private readonly ErrorLog? _log;

    public JsonFileStore(AppDataPaths paths, ErrorLog? log = null)
    {
        _paths = paths;
        _log = log;
    }

    public bool Exists(string fileName) => System.IO.File.Exists(_paths.File(fileName));

    public T Load<T>(string fileName, T fallback)
    {
        var path = _paths.File(fileName);
        if (!System.IO.File.Exists(path)) return fallback;

        string json;
        try
        {
            json = System.IO.File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Write(ex, $"Could not open {fileName}; using defaults for now.");
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? fallback;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            var aside = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            _log?.Write(ex, $"Could not read {fileName}; moved it to {Path.GetFileName(aside)} and used defaults.");
            TryMove(path, aside);
            return fallback;
        }
    }

    public void Save<T>(string fileName, T value)
    {
        Directory.CreateDirectory(_paths.Root);
        var path = _paths.File(fileName);
        var temp = path + ".tmp";

        using (var stream = System.IO.File.Create(temp))
        {
            JsonSerializer.Serialize(stream, value, SerializerOptions);
            stream.Flush(flushToDisk: true);
        }

        System.IO.File.Move(temp, path, overwrite: true);
    }

    public void Delete(string fileName)
    {
        var path = _paths.File(fileName);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private static void TryMove(string from, string to)
    {
        try { System.IO.File.Move(from, to, overwrite: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
