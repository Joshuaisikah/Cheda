using System.Security.Cryptography;

namespace Cheda.Core.Security;

/// <summary>
/// PIN-stretching and key-derivation service.
///
/// Pipeline:
///   1. PBKDF2-SHA256 (100 000 iterations) — slow, brute-force-resistant root key.
///   2. HKDF-Expand (SHA256) — derives two domain-separated 32-byte subkeys:
///        "auth" → verifier key  (stored; compared to verify the PIN)
///        "db"   → database key  (never stored; used to open the SQLCipher database)
///
/// Note on Keystore pepper: Android SecureStorage already encrypts values with a
/// hardware-backed Keystore key on API 23+, so the verifier is Keystore-protected
/// at rest. An explicit pepper XOR over the PBKDF2 output is a planned Phase 11
/// enhancement that adds a second Keystore key boundary.
/// </summary>
public sealed class PinHashService
{
    private const int Iterations = 100_000;
    private const int KeyBytes   = 32;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives both the verifier key and the DB encryption key from a PIN and salt.
    /// Returned keys are independent thanks to HKDF domain separation.
    /// </summary>
    public (byte[] VerifierKey, byte[] DbKey) DeriveKeys(string pin, byte[] salt)
    {
        var rootKey     = Pbkdf2(pin, salt);
        var verifierKey = HKDF.Expand(HashAlgorithmName.SHA256, rootKey, KeyBytes, "auth"u8.ToArray());
        var dbKey       = HKDF.Expand(HashAlgorithmName.SHA256, rootKey, KeyBytes, "db"u8.ToArray());
        return (verifierKey, dbKey);
    }

    /// <summary>
    /// Generates a new salt, derives both keys, and returns:
    ///   - <c>verifier</c>: the blob to store ("v1:{salt_b64}:{verifierKey_b64}")
    ///   - <c>dbKey</c>: the DB encryption key (use immediately, never store)
    /// </summary>
    public (string Verifier, byte[] DbKey) CreateVerifierAndDbKey(string pin)
    {
        var salt = GenerateSalt();
        var (verifierKey, dbKey) = DeriveKeys(pin, salt);
        var verifier = $"v1:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(verifierKey)}";
        return (verifier, dbKey);
    }

    /// <summary>
    /// Verifies a PIN against a stored verifier blob.
    /// Returns <c>(true, dbKey)</c> on match using constant-time comparison.
    /// Returns <c>(false, null)</c> on mismatch or malformed verifier.
    /// </summary>
    public (bool IsValid, byte[]? DbKey) Verify(string pin, string verifier)
    {
        var parts = verifier.Split(':');
        if (parts.Length != 3 || parts[0] != "v1") return (false, null);
        try
        {
            var salt       = Convert.FromBase64String(parts[1]);
            var storedKey  = Convert.FromBase64String(parts[2]);
            var (candidateKey, dbKey) = DeriveKeys(pin, salt);
            var ok = CryptographicOperations.FixedTimeEquals(storedKey, candidateKey);
            return (ok, ok ? dbKey : null);
        }
        catch
        {
            return (false, null);
        }
    }

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(32);

    // ── Internal ──────────────────────────────────────────────────────────────

    private static byte[] Pbkdf2(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password:      pin,
            salt:          salt,
            iterations:    Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength:  KeyBytes);
}
