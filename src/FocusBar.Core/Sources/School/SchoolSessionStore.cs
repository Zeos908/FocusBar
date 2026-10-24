using System.Text.Json;
using FocusBar.Core.Security;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Sources.School;

/// <summary>The saved school sign-in: the website's session cookies, plus who they belong to.</summary>
public record SchoolSession(string Host, string CookieHeader, string? DisplayName, DateTimeOffset SavedAt);

/// <summary>
/// Saves the school sign-in to <c>school-session.json</c>, encrypted (Windows DPAPI in the app), because the
/// session cookies are as sensitive as a password while they're valid. Nothing readable is stored.
/// </summary>
public sealed class SchoolSessionStore
{
    public const string FileName = "school-session.json";

    private readonly JsonFileStore _store;
    private readonly ISecretProtector _protector;

    public SchoolSessionStore(JsonFileStore store, ISecretProtector protector)
    {
        _store = store;
        _protector = protector;
    }

    /// <summary>The saved session, or null if there isn't one (or it can't be decrypted on this PC/user).</summary>
    public SchoolSession? Load()
    {
        var file = _store.Load<SessionFile?>(FileName, null);
        if (string.IsNullOrEmpty(file?.Data)) return null;

        try
        {
            var session = JsonSerializer.Deserialize<SchoolSession>(_protector.Unprotect(file.Data), JsonFileStore.SerializerOptions);
            return string.IsNullOrWhiteSpace(session?.CookieHeader) ? null : session;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Encrypted by another Windows user, copied from another PC, or damaged: treat as signed out.
            return null;
        }
    }

    public void Save(SchoolSession session) =>
        _store.Save(FileName, new SessionFile(_protector.Protect(JsonSerializer.Serialize(session, JsonFileStore.SerializerOptions))));

    public void Clear() => _store.Delete(FileName);

    private sealed record SessionFile(string Data, int Version = 1);
}
