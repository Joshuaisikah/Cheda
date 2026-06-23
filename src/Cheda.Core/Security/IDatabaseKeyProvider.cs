namespace Cheda.Core.Security;

/// <summary>
/// Provides the SQLCipher database encryption key, derived from the user's PIN
/// and held in memory until the app is locked or backgrounded.
/// </summary>
public interface IDatabaseKeyProvider
{
    /// <summary>Returns the cached key, or null if the user has not yet authenticated.</summary>
    byte[]? GetKey();

    /// <summary>Caches the key after a successful PIN or biometric auth.</summary>
    void SetKey(byte[] key);

    /// <summary>Zeroes and discards the key on lock or background.</summary>
    void ClearKey();
}
