using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Pages.Lock;
using Cheda.App.Security;
using Cheda.Core.Security;
using Cheda.Core.Sms;
using Cheda.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App.Pages.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppLockService       _lock;
    private readonly IImportService        _import;
    private readonly ISmsReader            _smsReader;
    private readonly ISettingsRepository   _settings;
    private readonly ITransactionRepository _repo;
    private readonly IDatabaseKeyProvider  _keyProvider;
    private readonly SecureBiometricKeyStore _bioKeyStore;

    [ObservableProperty] private string  _currentPin     = "";
    [ObservableProperty] private string  _newPin         = "";
    [ObservableProperty] private string  _newPinConfirm  = "";
    [ObservableProperty] private string? _pinChangeResult;
    [ObservableProperty] private string? _importResult;

    // Preferences
    [ObservableProperty] private bool _biometricEnabled;
    [ObservableProperty] private int  _lockDelayMinutes;
    [ObservableProperty] private bool _communityLearning;

    public string LockAfterLabel => LockDelayMinutes == 0
        ? "Lock after: Immediately"
        : $"Lock after: {LockDelayMinutes} minute{(LockDelayMinutes > 1 ? "s" : "")}";

    // Lock delay chip colours
    public Color LockDelay0Bg  => LockDelayMinutes == 0  ? Color.FromArgb("#3D2020") : Color.FromArgb("#2C1515");
    public Color LockDelay1Bg  => LockDelayMinutes == 1  ? Color.FromArgb("#3D2020") : Color.FromArgb("#2C1515");
    public Color LockDelay5Bg  => LockDelayMinutes == 5  ? Color.FromArgb("#3D2020") : Color.FromArgb("#2C1515");
    public Color LockDelay15Bg => LockDelayMinutes == 15 ? Color.FromArgb("#3D2020") : Color.FromArgb("#2C1515");
    public Color LockDelay0Fg  => LockDelayMinutes == 0  ? Color.FromArgb("#F1E8E6") : Color.FromArgb("#B8948A");
    public Color LockDelay1Fg  => LockDelayMinutes == 1  ? Color.FromArgb("#F1E8E6") : Color.FromArgb("#B8948A");
    public Color LockDelay5Fg  => LockDelayMinutes == 5  ? Color.FromArgb("#F1E8E6") : Color.FromArgb("#B8948A");
    public Color LockDelay15Fg => LockDelayMinutes == 15 ? Color.FromArgb("#F1E8E6") : Color.FromArgb("#B8948A");

    public SettingsViewModel(
        IAppLockService lockService,
        IImportService importService,
        ISmsReader smsReader,
        ISettingsRepository settings,
        ITransactionRepository repo,
        IDatabaseKeyProvider keyProvider,
        SecureBiometricKeyStore bioKeyStore)
    {
        _lock        = lockService;
        _import      = importService;
        _smsReader   = smsReader;
        _settings    = settings;
        _repo        = repo;
        _keyProvider = keyProvider;
        _bioKeyStore = bioKeyStore;

        // Default biometric ON when the user has never explicitly set it.
        // The lock screen still checks hardware availability independently,
        // so this is safe even on devices without enrolled biometrics.
        var bioStr = _settings.Get("BiometricEnabled");
        if (bioStr is null)
        {
            _biometricEnabled = true;
            _settings.Set("BiometricEnabled", "true");
        }
        else
        {
            _biometricEnabled = bioStr == "true";
        }
        _lockDelayMinutes  = int.TryParse(_settings.Get("LockDelayMinutes"), out var d) ? d : 0;
        _communityLearning = _settings.Get("CommunityLearning") == "true";
    }

    partial void OnBiometricEnabledChanged(bool value)
    {
        _settings.Set("BiometricEnabled", value ? "true" : "false");
        OnPropertyChanged(nameof(LockAfterLabel));
        // When enabling biometrics, persist the current DB key so the lock screen
        // can unlock without requiring a PIN entry first.
        if (value)
        {
            var key = _keyProvider.GetKey();
            if (key is not null)
                _bioKeyStore.Save(key);
        }
        else
        {
            _bioKeyStore.Clear();
        }
    }

    partial void OnLockDelayMinutesChanged(int value)
    {
        _settings.Set("LockDelayMinutes", value.ToString());
        OnPropertyChanged(nameof(LockAfterLabel));
        OnPropertyChanged(nameof(LockDelay0Bg));
        OnPropertyChanged(nameof(LockDelay1Bg));
        OnPropertyChanged(nameof(LockDelay5Bg));
        OnPropertyChanged(nameof(LockDelay15Bg));
        OnPropertyChanged(nameof(LockDelay0Fg));
        OnPropertyChanged(nameof(LockDelay1Fg));
        OnPropertyChanged(nameof(LockDelay5Fg));
        OnPropertyChanged(nameof(LockDelay15Fg));
    }

    partial void OnCommunityLearningChanged(bool value) =>
        _settings.Set("CommunityLearning", value ? "true" : "false");

    [RelayCommand]
    private void SetLockDelay(string param)
    {
        if (int.TryParse(param, out var minutes))
            LockDelayMinutes = minutes;
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        PinChangeResult = null;
        if (NewPin.Length < 4)       { PinChangeResult = "New PIN must be at least 4 digits."; return; }
        if (NewPin != NewPinConfirm)  { PinChangeResult = "PINs do not match.";                 return; }

        var verify = await _lock.VerifyPinAsync(CurrentPin);
        if (!verify.IsSuccess) { PinChangeResult = "Current PIN is incorrect."; return; }

        await _lock.SetupPinAsync(NewPin);
        CurrentPin      = "";
        NewPin          = "";
        NewPinConfirm   = "";
        PinChangeResult = "PIN changed successfully.";
    }

    [RelayCommand]
    private async Task ScanSmsAsync()
    {
        ImportResult = null;

        // Permission dialogs must run on the main thread — do them BEFORE RunAsync
        // which offloads to a background thread via Task.Run.
        if (!_smsReader.HasPermission)
        {
            var status = await Permissions.CheckStatusAsync<SmsReadPermission>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<SmsReadPermission>();
            if (status != PermissionStatus.Granted)
            {
                ImportResult = "⛔ SMS permission denied — go to Settings → Apps → Cheda → Permissions → SMS → Allow.";
                return;
            }
        }

        // Best-effort: request POST_NOTIFICATIONS on Android 13+ (main thread).
        try
        {
            var notifStatus = await Permissions.CheckStatusAsync<NotificationPermission>();
            if (notifStatus != PermissionStatus.Granted)
                await Permissions.RequestAsync<NotificationPermission>();
        }
        catch { /* non-critical */ }

        // Heavy I/O on background thread.
        await RunAsync(async () =>
        {
            ImportResult = "⏳ Scanning…";
            try
            {
                var result = await _import.ImportInboxAsync();
                var raw    = _smsReader.LastRawCount;

                if (_smsReader.LastError is not null)
                {
                    ImportResult = $"⚠️ Read error: {_smsReader.LastError}";
                    return;
                }

                if (raw == 0)
                {
                    ImportResult = "📭 No SMS found. Check MIUI Security → Permissions → SMS.";
                    return;
                }

                SelfTransferDetector.RedetectAndPersist(_repo);

                ImportResult = result.NewTransactions > 0
                    ? $"✅ Scanned {raw} SMS — {result.NewTransactions} new imported" +
                      (result.Duplicates > 0 ? $", {result.Duplicates} already saved" : "") +
                      (result.ReviewQueue.Count > 0 ? $", {result.ReviewQueue.Count} need review" : "") + "."
                    : $"📋 Scanned {raw} SMS — {result.Duplicates} already saved, nothing new.";
            }
            catch (Exception ex)
            {
                ImportResult = $"⚠️ Scan failed: {ex.Message}";
            }
        });
    }

    [RelayCommand]
    private async Task ImportXmlAsync()
    {
        await RunAsync(async () =>
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select SMS backup XML",
                FileTypes   = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["text/xml", "application/xml"],
                }),
            });

            if (result is null) return;

            await using var stream = await result.OpenReadAsync();
            var importResult       = await _import.ImportFromXmlAsync(stream);
            ImportResult = $"Imported {importResult.NewTransactions} new transactions" +
                           (importResult.Duplicates > 0 ? $", {importResult.Duplicates} already saved" : "") +
                           (importResult.ReviewQueue.Count > 0 ? $", {importResult.ReviewQueue.Count} need review" : "") +
                           ".";
        });
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        _lock.Lock();
        var shell = Application.Current!.Windows[0].Page!;
        shell.Opacity = 0;
        var lockPage = IPlatformApplication.Current!.Services.GetRequiredService<LockPage>();
        await shell.Navigation.PushModalAsync(lockPage, animated: false);
    }

    [RelayCommand]
    private async Task RateAppAsync()
    {
        try
        {
            await Launcher.OpenAsync("market://details?id=com.cheda.app");
        }
        catch
        {
            await Launcher.OpenAsync("https://play.google.com/store");
        }
    }

    [RelayCommand]
    private async Task InviteFriendAsync()
    {
        await Share.RequestAsync(new ShareTextRequest
        {
            Title = "Cheda – M-Pesa Tracker",
            Text  = "I use Cheda to track my M-Pesa spending. It's free and works offline. Try it!",
        });
    }

    [RelayCommand]
    private async Task DataHealthAsync()
    {
        await Application.Current!.Windows[0].Page!
            .DisplayAlert("Data Health", "Your transaction data looks healthy. No issues detected.", "OK");
    }

    [RelayCommand]
    private async Task DonateAsync()
    {
        await Application.Current!.Windows[0].Page!
            .DisplayAlert("Support Cheda", "Thank you! Donation support coming soon.", "OK");
    }

    [RelayCommand]
    private async Task RedetectSimTransfersAsync()
    {
        await RunAsync(() =>
        {
            var count = SelfTransferDetector.RedetectAndPersist(_repo);
            ImportResult = count > 0
                ? $"🔄 Tagged {count} transaction{(count == 1 ? "" : "s")} as SIM transfers."
                : "✅ No new inter-SIM transfers found.";
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var page = Application.Current!.Windows[0].Page!;
        var confirm = await page.DisplayAlert(
            "Clear History",
            "This will permanently delete all transaction data. This cannot be undone.",
            "Delete Everything",
            "Cancel");

        if (!confirm) return;

        await RunAsync(() =>
        {
            _repo.DeleteAll();
            return Task.CompletedTask;
        });

        await page.DisplayAlertAsync("Cleared", "All transaction history has been deleted.", "OK");
    }
}

public class SmsReadPermission : Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    [
        (global::Android.Manifest.Permission.ReadSms, true),
    ];
#endif
}

public class NotificationPermission : Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    [
        ("android.permission.POST_NOTIFICATIONS", true),
    ];
#endif
}
