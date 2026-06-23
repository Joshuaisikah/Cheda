using Microsoft.Maui.Storage;

namespace Cheda.App.Security;

/// <summary>
/// Stores a copy of the PIN-derived DB key in SecureStorage so that biometric
/// unlock can retrieve it without requiring the PIN again.
/// </summary>
public sealed class SecureBiometricKeyStore
{
    private const string Key = "cheda_biometric_db_key";

    public void Save(byte[] key)
    {
        try { SecureStorage.SetAsync(Key, Convert.ToBase64String(key)).GetAwaiter().GetResult(); }
        catch { /* best effort */ }
    }

    public byte[]? Load()
    {
        try
        {
            var s = SecureStorage.GetAsync(Key).GetAwaiter().GetResult();
            return s is null ? null : Convert.FromBase64String(s);
        }
        catch { return null; }
    }

    public void Clear()
    {
        try { SecureStorage.Remove(Key); }
        catch { }
    }
}
