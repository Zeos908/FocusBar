using System.Security.Cryptography;
using System.Text;
using FocusBar.Core.Security;

namespace FocusBar.App.Security;

/// <summary>
/// Encrypts secrets with Windows DPAPI for the current Windows user (as the build instructions require).
/// Only this Windows account on this PC can decrypt them; copying the file elsewhere doesn't reveal anything.
/// </summary>
internal sealed class DpapiProtector : ISecretProtector
{
    // Extra input mixed into the encryption so other programs using DPAPI can't trivially decrypt FocusBar's data.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FocusBar/secrets/v1");

    public string Protect(string plainText) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser));

    public string Unprotect(string protectedText) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser));
}
