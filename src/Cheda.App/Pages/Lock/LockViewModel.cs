using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Security;
using Cheda.App.Storage;
using Cheda.Core.Security;
using Cheda.Core.Storage;

namespace Cheda.App.Pages.Lock;

public partial class LockViewModel : ViewModelBase
{
    private readonly IAppLockService         _lock;
    private readonly IBiometricService?      _bio;
    private readonly SecureBiometricKeyStore _bioKeyStore;
    private readonly IDatabaseKeyProvider    _keyProvider;
    private readonly DatabaseService         _db;
    private readonly ISettingsRepository     _settings;

    [ObservableProperty] private string  _pinDisplay  = "";
    [ObservableProperty] private string? _lockMessage;
    [ObservableProperty] private bool    _biometricAvailable;
    [ObservableProperty] private bool    _showingPinPad;
    [ObservableProperty] private bool    _isWaiting   = true;  // shows spinner while biometric fires
    [ObservableProperty] private bool    _showingFingerprintRetry; // shows after a failed attempt

    private string  _pin = "";
    private int     _biometricInProgress; // Interlocked guard — prevents double-prompt on MIUI
    private bool    _initialized;         // ensures auto-biometric fires only once per page instance
    private bool    _dismissed;           // set after successful auth — blocks all MIUI late callbacks
    public  bool    IsDismissed => _dismissed;
    private byte[]? _cachedBioKey;        // pre-loaded while lock screen shows to avoid post-auth delay

    public LockViewModel(
        IAppLockService lockService,
        SecureBiometricKeyStore bioKeyStore,
        IDatabaseKeyProvider keyProvider,
        DatabaseService db,
        ISettingsRepository settings,
        IBiometricService? biometricService = null)
    {
        _lock        = lockService;
        _bioKeyStore = bioKeyStore;
        _keyProvider = keyProvider;
        _db          = db;
        _settings    = settings;
        _bio         = biometricService;
    }

    public async Task InitializeAsync()
    {
        if (_dismissed) return;
        if (_initialized) return; // fire biometric only once per instance — MIUI re-entries ignored
        _initialized       = true;
        var bioEnabled = _settings.Get("BiometricEnabled") != "false"; // default ON when not yet set
        BiometricAvailable = (_bio?.IsAvailable ?? false) && bioEnabled;

        if (!BiometricAvailable)
        {
            IsWaiting     = false;
            ShowingPinPad = true;
            return;
        }

        // Pre-load key in background while spinner shows (~200ms Android Keystore read).
        _ = Task.Run(() => _cachedBioKey = _bioKeyStore.Load());
        await TryBiometricAsync();
    }

    [RelayCommand]
    private void SwitchToPin()
    {
        ShowingPinPad = true;
        LockMessage   = null;
    }

    [RelayCommand]
    private async Task SwitchToBiometricAsync()
    {
        IsWaiting               = true;
        ShowingPinPad           = false;
        ShowingFingerprintRetry = false;
        LockMessage             = null;
        System.Threading.Interlocked.Exchange(ref _biometricInProgress, 0);
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
            // Persist DB key so biometric unlock can use it next time
            var key = _keyProvider.GetKey();
            if (key is not null)
                _bioKeyStore.Save(key);

            _dismissed = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                PinDisplay = "";
                await DismissModal();
            });
            _ = Task.Run(() => _db.InitializeAsync());
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
        // MIUI's BiometricPrompt briefly pauses the activity, which fires OnAppearing again.
        // The Interlocked compare-exchange ensures only one prompt is ever active at a time.
        if (System.Threading.Interlocked.CompareExchange(ref _biometricInProgress, 1, 0) != 0)
            return;

        try
        {
            var result = await _bio.AuthenticateAsync("Unlock Cheda");

            if (!result.IsSuccess)
            {
                if (result.IsPinFallback)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsWaiting     = false;
                        ShowingPinPad = true;
                    });
                }
                else
                {
                    // Show the fingerprint retry UI with an error message.
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsWaiting              = false;
                        ShowingFingerprintRetry = true;
                        LockMessage            = result.ErrorMessage ?? "Tap the icon to try again.";
                    });
                }
                return;
            }

            var dbKey = _cachedBioKey ?? _bioKeyStore.Load();
            if (dbKey is null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowingPinPad = true;
                    LockMessage   = "Enter your PIN to set up fingerprint unlock.";
                });
                return;
            }

            if (_dismissed) return;
            _dismissed = true;
            _lock.Unlock(dbKey);
            await MainThread.InvokeOnMainThreadAsync(async () => await DismissModal());
            _ = Task.Run(() => _db.InitializeAsync());
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _biometricInProgress, 0);
        }
    }

    private static Task DismissModal()
    {
        var window = Application.Current!.Windows[0];

        if (window.Page?.Navigation.ModalStack.Count > 0)
        {
            var modal = window.Page.Navigation.ModalStack[^1];
            modal.Opacity        = 0;
            window.Page!.Opacity = 1;
            return window.Page.Navigation.PopModalAsync(animated: false);
        }

        if (window.Page != null) window.Page.Opacity = 0;
        var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
        window.Page = shell;
        return Task.CompletedTask;
    }
}
