using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cheda.App.Pages.Lock;
using Cheda.App.Security;
using Cheda.Core.Security;
using Cheda.Core.Sms;
using Microsoft.Extensions.DependencyInjection;

namespace Cheda.App.Pages.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppLockService _lock;
    private readonly IImportService  _import;
    private readonly ISmsReader      _smsReader;

    [ObservableProperty] private string  _currentPin     = "";
    [ObservableProperty] private string  _newPin         = "";
    [ObservableProperty] private string  _newPinConfirm  = "";
    [ObservableProperty] private string? _pinChangeResult;
    [ObservableProperty] private string? _importResult;

    public SettingsViewModel(IAppLockService lockService, IImportService importService, ISmsReader smsReader)
    {
        _lock      = lockService;
        _import    = importService;
        _smsReader = smsReader;
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

    // Reads M-Pesa messages directly from the phone's SMS inbox.
    [RelayCommand]
    private async Task ScanSmsAsync()
    {
        await RunAsync(async () =>
        {
            // Request READ_SMS permission if not yet granted.
            if (!_smsReader.HasPermission)
            {
                var status = await Permissions.CheckStatusAsync<SmsReadPermission>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<SmsReadPermission>();
                if (status != PermissionStatus.Granted)
                {
                    ImportResult = "⛔ Permission denied — go to Settings → Apps → Cheda → Permissions → SMS → Allow.";
                    return;
                }
            }

            var result = await _import.ImportInboxAsync();

            // Surface diagnostic info so the user knows what happened.
            var raw = _smsReader.LastRawCount;
            if (_smsReader.LastError is not null)
            {
                ImportResult = $"⚠️ Read error: {_smsReader.LastError}";
                return;
            }

            if (raw == 0)
            {
                ImportResult = "📭 No SMS found. Your phone may need SMS permission in MIUI Security app too (Authorizations → SMS → Allow).";
                return;
            }

            ImportResult = result.NewTransactions > 0
                ? $"✅ Scanned {raw} SMS — imported {result.NewTransactions} new" +
                  (result.Duplicates > 0 ? $", {result.Duplicates} already saved" : "") +
                  (result.ReviewQueue.Count > 0 ? $", {result.ReviewQueue.Count} need review" : "") + "."
                : $"📋 Scanned {raw} SMS — {result.Duplicates} already saved, nothing new.";
        });
    }

    // Keep XML import as fallback.
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
}

// MAUI custom permission for READ_SMS (not built-in to MAUI Essentials).
public class SmsReadPermission : Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
    [
        (global::Android.Manifest.Permission.ReadSms, true),
    ];
#endif
}
