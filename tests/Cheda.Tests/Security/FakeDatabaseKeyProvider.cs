using Cheda.Core.Security;

namespace Cheda.Tests.Security;

public sealed class FakeDatabaseKeyProvider : IDatabaseKeyProvider
{
    public byte[]? Key      { get; private set; }
    public int     SetCount { get; private set; }
    public int     ClearCount { get; private set; }

    public byte[]? GetKey() => Key;

    public void SetKey(byte[] key)
    {
        Key = key.ToArray();
        SetCount++;
    }

    public void ClearKey()
    {
        Key = null;
        ClearCount++;
    }
}
