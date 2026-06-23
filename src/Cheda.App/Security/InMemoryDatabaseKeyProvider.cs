using System.Security.Cryptography;
using Cheda.Core.Security;

namespace Cheda.App.Security;

/// <summary>
/// Holds the PIN-derived DB key in memory for the life of the authenticated session.
/// On Lock() the key bytes are explicitly zeroed before the reference is discarded.
/// </summary>
public sealed class InMemoryDatabaseKeyProvider : IDatabaseKeyProvider
{
    private byte[]? _key;

    public byte[]? GetKey() => _key;

    public void SetKey(byte[] key)
    {
        // Copy to isolate from any mutation of the caller's buffer.
        var copy = new byte[key.Length];
        key.CopyTo(copy, 0);
        _key = copy;
    }

    public void ClearKey()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }
}
