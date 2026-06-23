namespace Cheda.Core.Security;

public interface IBiometricService
{
    /// <summary>True if biometric hardware is present and at least one credential is enrolled.</summary>
    bool IsAvailable { get; }

    Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default);
}
