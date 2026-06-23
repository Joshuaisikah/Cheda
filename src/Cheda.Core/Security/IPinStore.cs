namespace Cheda.Core.Security;

/// <summary>
/// Persists the PIN verifier blob ("v1:{salt_b64}:{key_b64}").
/// Android implementation uses SecureStorage (Keystore-backed on API 23+).
/// </summary>
public interface IPinStore
{
    bool    HasPin { get; }
    string? Load();
    void    Save(string verifier);
    void    Clear();
}
