using FocusBar.Core.Security;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Sources;

/// <summary>
/// Saves the student's calendars to <c>calendars.json</c>. Each link or file path is encrypted, because a private
/// calendar link (like Google Calendar's "secret address") lets anyone who has it read the calendar.
/// </summary>
public sealed class CalendarFeedStore
{
    public const string FileName = "calendars.json";

    private readonly JsonFileStore _store;
    private readonly ISecretProtector _protector;

    public CalendarFeedStore(JsonFileStore store, ISecretProtector protector)
    {
        _store = store;
        _protector = protector;
    }

    public IReadOnlyList<CalendarFeed> Load()
    {
        var file = _store.Load(FileName, new FeedFile([]));
        var feeds = new List<CalendarFeed>();
        foreach (var saved in file.Feeds ?? [])
        {
            if (string.IsNullOrWhiteSpace(saved.Name) || string.IsNullOrWhiteSpace(saved.Location)) continue;
            try
            {
                feeds.Add(new CalendarFeed(saved.Name, _protector.Unprotect(saved.Location), saved.ClassName));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Can't be decrypted on this PC/user; skip it rather than fail.
            }
        }
        return feeds;
    }

    public void Save(IEnumerable<CalendarFeed> feeds) =>
        _store.Save(FileName, new FeedFile([.. feeds.Select(f => new SavedFeed(f.Name, _protector.Protect(f.Location), f.ClassName))]));

    private sealed record SavedFeed(string Name, string Location, string? ClassName);

    private sealed record FeedFile(List<SavedFeed> Feeds, int Version = 1);
}
