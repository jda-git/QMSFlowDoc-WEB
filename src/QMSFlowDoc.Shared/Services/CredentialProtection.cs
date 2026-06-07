using System.Security.Cryptography;
using System.Text;

namespace QMSFlowDoc.Shared.Services;

/// <summary>
/// Provides DPAPI-based encryption/decryption for sensitive credentials
/// stored in serversettings.json. Uses LocalMachine scope so that all
/// services running on the same server can access the encrypted data.
/// </summary>
public static class CredentialProtection
{
    private const string EncryptedPrefix = "DPAPI:";

    /// <summary>
    /// Encrypts a plain-text password using DPAPI (LocalMachine scope).
    /// Returns a Base64-encoded string prefixed with "DPAPI:".
    /// </summary>
    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (IsEncrypted(plainText))
            return plainText; // Already encrypted

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);

        return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted password string.
    /// If the string is not encrypted (no "DPAPI:" prefix), returns it as-is
    /// for backward compatibility with legacy plain-text passwords.
    /// </summary>
    public static string DecryptPassword(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;

        if (!IsEncrypted(encryptedText))
            return encryptedText; // Legacy plain-text password, return as-is

        var base64 = encryptedText.Substring(EncryptedPrefix.Length);
        var encryptedBytes = Convert.FromBase64String(base64);
        var plainBytes = ProtectedData.Unprotect(
            encryptedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Checks whether a value is already DPAPI-encrypted.
    /// </summary>
    public static bool IsEncrypted(string value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }
}
