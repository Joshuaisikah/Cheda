using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Security;
using Cheda.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App.Pages.Lock;

public partial class LockViewModel : ViewModelBase
{
    private readonly IAppLockService       _lock;
    private readonly IBiometricService?    _bio;
    private readonly SecureBiometricKeyStore _bioKeyStore;

    [ObservableProperty] private string  _pinDisplay  = "";
    [ObservableProperty] private string? _lockMessage;
    [ObservableProperty] private bool    _biometricAvailable;

    private string _pin = "";

    public LockViewModel(
        IAppLockService lockService,
        SecureBiometricKeyStore bioKeyStore,
        IBiometricService? biometricService = null)
    {
        _lock        = lockService;
        _bioKeyStore = bioKeyStore;
        _bio         = biometricService;
    }

    public async Task InitializeAsync()
    {
        BiometricAvailable = _bio?.IsAvailable ?? false;
        if (BiometricAvailable)
            await TryBiometricAsync();
    }

    [RelayCommand]
    private void PressDigit(string digit)
    {
        if (_pin.Length >= 6) return;
        _pin      += digit;
        PinDisplay = new string('●', _pin.Length);
        LockMessage = null;

        if (_pin.Length == 4 || _pin.Length == 6)
            _ = Task.Run(SubmitPinAsync);
    }

    [RelayCommand]
    private void Backspace()
    {
        if (_pin.Length == 0) return;
        _pin      = _pin[..^1];
        PinDisplay = new string('●', _pin.Length);
    }

    [RelayCommand]
    private async Task BiometricAsync() => await TryBiometricAsync();

    private async Task SubmitPinAsync()
    {
        var result = await _lock.VerifyPinAsync(_pin);
        _pin      = "";

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PinDisplay = "";
            if (result.IsSuccess)
                NavigateToShell();
            else
                LockMessage = "Incorrect PIN. Try again.";
        });
    }

    private async Task TryBiometricAsync()
    {
        if (_bio is null) return;
        var result = await _bio.AuthenticateAsync("Unlock Cheda");
        if (!result.IsSuccess) return;

        var dbKey = _bioKeyStore.Load();
        if (dbKey is null)
        {
            // No stored key — biometric can't unlock without the DB key.
            // Fall back to PIN entry.
            LockMessage = "Please enter your PIN.";
            return;
        }

        _lock.Unlock(dbKey);
        await MainThread.InvokeOnMainThreadAsync(NavigateToShell);
    }

    private static void NavigateToShell() =>
        Application.Current!.Windows[0].Page =
            IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
}
