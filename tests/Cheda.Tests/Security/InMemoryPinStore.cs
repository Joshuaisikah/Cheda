using Cheda.Core.Security;

namespace Cheda.Tests.Security;

public sealed class InMemoryPinStore : IPinStore
{
    private string? _verifier;

    public bool    HasPin       => _verifier is not null;
    public string? Load()       => _verifier;
    public void    Save(string v) => _verifier = v;
    public void    Clear()      => _verifier = null;
}
