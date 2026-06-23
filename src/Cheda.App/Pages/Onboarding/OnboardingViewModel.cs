using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Security;
using Cheda.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App.Pages.Onboarding;

public partial class OnboardingViewModel : ViewModelBase
{
    private readonly IAppLockService         _lock;
    private readonly IDatabaseKeyProvider    _keyProvider;
    private readonly SecureBiometricKeyStore _bioKeyStore;

    [ObservableProperty] private int     _step         = 0;
    [ObservableProperty] private string  _pin          = "";
    [ObservableProperty] private string  _pinConfirm   = "";
    [ObservableProperty] private string? _pinError;
    [ObservableProperty] private bool    _smsGranted;

    public OnboardingViewModel(
        IAppLockService lockService,
        IDatabaseKeyProvider keyProvider,
        SecureBiometricKeyStore bioKeyStore)
    {
        _lock        = lockService;
        _keyProvider = keyProvider;
        _bioKeyStore = bioKeyStore;
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == 0) { Step = 1; return; }
        if (Step == 1) { TrySetPin(); return; }
        if (Step == 2) { Finish(); }
    }

    [RelayCommand]
    private async Task RequestSmsPermissionAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Sms>();
        SmsGranted = status == PermissionStatus.Granted;
    }

    private void TrySetPin()
    {
        PinError = null;
        if (Pin.Length < 4)    { PinError = "PIN must be at least 4 digits."; return; }
        if (Pin != PinConfirm) { PinError = "PINs do not match.";              return; }

        _ = Task.Run(async () =>
        {
            await _lock.SetupPinAsync(Pin);

            // After setup, the PIN-derived DB key is cached in memory. Save it to
            // SecureStorage so biometric unlock can retrieve it without the PIN.
            var key = _keyProvider.GetKey();
            if (key is not null)
                _bioKeyStore.Save(key);

            await MainThread.InvokeOnMainThreadAsync(() => Step = 2);
        });
    }

    private static void Finish() =>
        Application.Current!.Windows[0].Page =
            IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
}
