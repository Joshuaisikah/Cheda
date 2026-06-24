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
    // Use Task.Run to avoid blocking the UI thread (GetResult on async inside a sync property
    // causes a deadlock on the main thread on some MIUI versions).
    // Allow alternative authentication (face/fingerprint) — MIUI may report fingerprint
    // under allowAlternativeAuthentication:true even when strict biometric returns false.
    public bool IsAvailable
    {
        get
        {
            try
            {
                return Task.Run(async () =>
                    await CrossFingerprint.Current.IsAvailableAsync(
                        allowAlternativeAuthentication: true))
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
                AllowAlternativeAuthentication = false, // prompt shows biometric only
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
