using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Security;
using Cheda.App.Storage;
using Cheda.Core.Security;

namespace Cheda.App.Pages.Onboarding;

public partial class OnboardingViewModel : ViewModelBase
{
    private readonly IAppLockService         _lock;
    private readonly IDatabaseKeyProvider    _keyProvider;
    private readonly SecureBiometricKeyStore _bioKeyStore;
    private readonly DatabaseService         _db;

    [ObservableProperty] private int     _step         = 0;
    [ObservableProperty] private string  _pin          = "";
    [ObservableProperty] private string  _pinConfirm   = "";
    [ObservableProperty] private string? _pinError;
    [ObservableProperty] private bool    _smsGranted;

    public string ButtonLabel => Step switch
    {
        0 => "Get Started",
        1 => "Set PIN",
        _ => "Continue"
    };

    public OnboardingViewModel(
        IAppLockService lockService,
        IDatabaseKeyProvider keyProvider,
        SecureBiometricKeyStore bioKeyStore,
        DatabaseService db)
    {
        _lock        = lockService;
        _keyProvider = keyProvider;
        _bioKeyStore = bioKeyStore;
        _db          = db;
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == 0) { Step = 1; OnPropertyChanged(nameof(ButtonLabel)); return; }
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
            try
            {
                await _lock.SetupPinAsync(Pin);

                // DB key is now in memory — open the database with it.
                await _db.InitializeAsync();

                // Save key to SecureStorage for biometric unlock.
                var key = _keyProvider.GetKey();
                if (key is not null)
                    _bioKeyStore.Save(key);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Step = 2;
                    OnPropertyChanged(nameof(ButtonLabel));
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    PinError = $"Setup failed: {ex.Message}");
            }
        });
    }

    private static async void Finish()
    {
        var window = Application.Current!.Windows[0];

        if (window.Page?.Navigation.ModalStack.Count > 0)
        {
            var shell = window.Page!;
            await shell.Navigation.PopModalAsync(animated: false);
            await shell.FadeToAsync(1, 250, Easing.CubicOut);
        }
        else
        {
            // Startup case: OnboardingPage IS the Window page — swap to Shell.
            var shell   = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
            shell.Opacity = 0;
            window.Page = shell;
            await shell.FadeToAsync(1, 300, Easing.CubicOut);
        }
    }
}
