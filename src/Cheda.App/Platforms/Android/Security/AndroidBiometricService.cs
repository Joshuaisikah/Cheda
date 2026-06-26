using Android.Content;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Cheda.Core.Security;
using Java.Lang;

namespace Cheda.App.Platforms.Android.Security;

/// <summary>
/// Biometric authentication using AndroidX BiometricPrompt directly.
/// Uses the no-arg CanAuthenticate() (available in 1.0.0) which accepts any enrolled
/// biometric — including in-display optical / ultrasonic fingerprint sensors.
/// </summary>
internal sealed class AndroidBiometricService : IBiometricService
{
    // BiometricPrompt error codes (stable Android API constants)
    private const int ErrorNegativeButton = 13;  // user tapped "Use PIN instead"
    private const int ErrorUserCanceled   = 10;  // user dismissed
    private const int ErrorCanceled       = 5;   // system cancelled (e.g. screen off)

    public bool IsAvailable
    {
        get
        {
            try
            {
                var mgr = BiometricManager.From(global::Android.App.Application.Context);
#pragma warning disable CS0618 // no-arg CanAuthenticate deprecated in 1.1.0, fine on 1.0.0
                return mgr.CanAuthenticate() == 0; // 0 = BIOMETRIC_SUCCESS
#pragma warning restore CS0618
            }
            catch { return false; }
        }
    }

    public async Task<BiometricResult> AuthenticateAsync(
        string reason, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<BiometricResult>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity;
            if (activity is not AndroidX.Fragment.App.FragmentActivity fa)
            {
                tcs.TrySetResult(BiometricResult.Error("Activity not available."));
                return;
            }

            var executor = ContextCompat.GetMainExecutor(fa);
            var callback  = new BioCallback(tcs);
            var prompt    = new BiometricPrompt(fa, executor, callback);

            var info = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Unlock Cheda")
                .SetSubtitle(reason)
                .SetNegativeButtonText("Use PIN instead")
                .SetConfirmationRequired(false)   // accept fingerprint immediately, no extra tap
                .Build();

            prompt.Authenticate(info);
        });

        using var reg = ct.Register(() => tcs.TrySetResult(BiometricResult.Cancelled));
        return await tcs.Task;
    }

    private sealed class BioCallback : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<BiometricResult> _tcs;
        public BioCallback(TaskCompletionSource<BiometricResult> tcs) => _tcs = tcs;

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) =>
            _tcs.TrySetResult(BiometricResult.Success);

        public override void OnAuthenticationError(int errMsgId, ICharSequence? errString)
        {
            // ErrorNegativeButton = user tapped "Use PIN instead" — explicit PIN choice.
            // ErrorUserCanceled / ErrorCanceled = dismissed without choosing — allow retry.
            if (errMsgId == ErrorNegativeButton)
            {
                _tcs.TrySetResult(BiometricResult.PinFallback);
                return;
            }
            var cancelled = errMsgId is ErrorUserCanceled or ErrorCanceled;
            _tcs.TrySetResult(cancelled
                ? BiometricResult.Cancelled
                : BiometricResult.Error(errString?.ToString() ?? "Biometric error"));
        }

        // Individual failed attempt — keep the prompt open (Android handles retries).
        public override void OnAuthenticationFailed() { }
    }
}
