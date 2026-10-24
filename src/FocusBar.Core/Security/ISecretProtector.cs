namespace FocusBar.Core.Security;

/// <summary>
/// Encrypts secrets (the school sign-in, private calendar links) before they're written to disk.
/// The app uses Windows DPAPI tied to the current Windows user; tests use a fake.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Returns an encrypted, Base64 version of <paramref name="plainText"/>.</summary>
    string Protect(string plainText);

    /// <summary>Reverses <see cref="Protect"/>. Throws if the data was encrypted by another Windows user or PC.</summary>
    string Unprotect(string protectedText);
}
