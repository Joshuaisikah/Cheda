using Cheda.Core.Security;

namespace Cheda.Tests.Security;

public sealed class FakeBiometricService : IBiometricService
{
    public bool IsAvailable { get; set; } = true;
    public BiometricResult NextResult { get; set; } = BiometricResult.Success;

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default) =>
        Task.FromResult(NextResult);
}
