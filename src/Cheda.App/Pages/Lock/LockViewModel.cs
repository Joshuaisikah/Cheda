using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Security;
using Cheda.App.Storage;
using Cheda.Core.Security;

namespace Cheda.App.Pages.Lock;

public partial class LockViewModel : ViewModelBase
{
    private readonly IAppLockService         _lock;
    private readonly IBiometricService?      _bio;
    private readonly SecureBiometricKeyStore _bioKeyStore;
    private readonly DatabaseService         _db;

    [ObservableProperty] private string  _pinDisplay  = "";
    [ObservableProperty] private string? _lockMessage;
    [ObservableProperty] private bool    _biometricAvailable;

    private string _pin = "";

    public LockViewModel(
        IAppLockService lockService,
        SecureBiometricKeyStore bioKeyStore,
        DatabaseService db,
        IBiometricService? biometricService = null)
    {
        _lock        = lockService;
        _bioKeyStore = bioKeyStore;
        _db          = db;
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
        _pin        += digit;
        PinDisplay   = new string('●', _pin.Length);
        LockMessage  = null;

        if (_pin.Length == 4 || _pin.Length == 6)
            _ = Task.Run(SubmitPinAsync);
    }

    [RelayCommand]
    private void Backspace()
    {
        if (_pin.Length == 0) return;
        _pin       = _pin[..^1];
        PinDisplay = new string('●', _pin.Length);
    }

    [RelayCommand]
    private async Task BiometricAsync() => await TryBiometricAsync();

    private async Task SubmitPinAsync()
    {
        var result = await _lock.VerifyPinAsync(_pin);
        _pin = "";

        if (result.IsSuccess)
        {
            await _db.InitializeAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                PinDisplay = "";
                await DismissModal();
            });
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PinDisplay  = "";
                LockMessage = "Incorrect PIN. Try again.";
            });
        }
    }

    private async Task TryBiometricAsync()
    {
        if (_bio is null) return;
        var result = await _bio.AuthenticateAsync("Unlock Cheda");
        if (!result.IsSuccess) return;

        var dbKey = _bioKeyStore.Load();
        if (dbKey is null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                LockMessage = "Please enter your PIN.");
            return;
        }

        _lock.Unlock(dbKey);
        await _db.InitializeAsync();
        await MainThread.InvokeOnMainThreadAsync(async () => await DismissModal());
    }

    private static async Task DismissModal()
    {
        var window = Application.Current!.Windows[0];

        if (window.Page?.Navigation.ModalStack.Count > 0)
        {
            // OnResume case: LockPage was pushed as a modal on top of AppShell.
            var shell = window.Page!;
            await shell.Navigation.PopModalAsync(animated: false);
            await shell.FadeToAsync(1, 250, Easing.CubicOut);
        }
        else
        {
            // Startup case: LockPage IS the Window page — swap to Shell.
            var shell   = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
            shell.Opacity = 0;
            window.Page = shell;
            await shell.FadeToAsync(1, 300, Easing.CubicOut);
        }
    }
}
