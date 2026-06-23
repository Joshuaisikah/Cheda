using Cheda.Core.Security;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace Cheda.App.Platforms.Android.Security;

/// <summary>
/// Biometric authentication backed by Plugin.Fingerprint (AndroidX BiometricPrompt).
/// Requires the app to be in the foreground — call only from a visible Activity context.
/// </summary>
internal sealed class AndroidBiometricService : IBiometricService
{
    public bool IsAvailable
    {
        get
        {
            try
            {
                return CrossFingerprint.Current
                    .IsAvailableAsync(allowAlternativeAuthentication: false)
                    .GetAwaiter().GetResult();
            }
            catch { return false; }
        }
    }

    public async Task<BiometricResult> AuthenticateAsync(
        string reason, CancellationToken ct = default)
    {
        try
        {
            var config = new AuthenticationRequestConfiguration("Unlock Cheda", reason)
            {
                AllowAlternativeAuthentication = false,
                ConfirmationRequired           = false,
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(config, ct);

            if (result.Authenticated)
                return BiometricResult.Success;

            return result.Status == FingerprintAuthenticationResultStatus.Canceled
                ? BiometricResult.Cancelled
                : BiometricResult.Error(result.ErrorMessage ?? "Authentication failed.");
        }
        catch (OperationCanceledException)
        {
            return BiometricResult.Cancelled;
        }
        catch (Exception ex)
        {
            return BiometricResult.Error(ex.Message);
        }
    }
}
