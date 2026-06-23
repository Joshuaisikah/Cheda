using Cheda.Core.Security;
using Microsoft.Maui.Storage;

namespace Cheda.App.Security;

/// <summary>
/// Persists the PIN verifier blob in Android SecureStorage.
/// On API 23+, SecureStorage uses a hardware-backed Keystore key, so the verifier
/// is protected by the device's Trusted Execution Environment at rest.
/// </summary>
public sealed class SecureStoragePinStore : IPinStore
{
    private const string VerifierKey = "cheda_pin_verifier";

    public bool HasPin
    {
        get
        {
            try { return SecureStorage.GetAsync(VerifierKey).GetAwaiter().GetResult() is not null; }
            catch { return false; }
        }
    }

    public string? Load()
    {
        try { return SecureStorage.GetAsync(VerifierKey).GetAwaiter().GetResult(); }
        catch { return null; }
    }

    public void Save(string verifier)
    {
        try { SecureStorage.SetAsync(VerifierKey, verifier).GetAwaiter().GetResult(); }
        catch { /* best effort; will fail open on next VerifyPin */ }
    }

    public void Clear()
    {
        try { SecureStorage.Remove(VerifierKey); }
        catch { }
    }
}
