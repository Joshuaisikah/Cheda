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

    [ObservableProperty] private string  _currentPin     = "";
    [ObservableProperty] private string  _newPin         = "";
    [ObservableProperty] private string  _newPinConfirm  = "";
    [ObservableProperty] private string? _pinChangeResult;
    [ObservableProperty] private string? _importResult;

    public SettingsViewModel(IAppLockService lockService, IImportService importService)
    {
        _lock   = lockService;
        _import = importService;
    }

    [RelayCommand]
    private async Task ChangePinAsync()
    {
        PinChangeResult = null;
        if (NewPin.Length < 4)      { PinChangeResult = "New PIN must be at least 4 digits."; return; }
        if (NewPin != NewPinConfirm) { PinChangeResult = "PINs do not match.";                 return; }

        var verify = await _lock.VerifyPinAsync(CurrentPin);
        if (!verify.IsSuccess) { PinChangeResult = "Current PIN is incorrect."; return; }

        await _lock.SetupPinAsync(NewPin);
        CurrentPin      = "";
        NewPin          = "";
        NewPinConfirm   = "";
        PinChangeResult = "PIN changed successfully.";
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
            ImportResult = $"Imported {importResult.NewTransactions} new, {importResult.Duplicates} duplicates, {importResult.ReviewQueue.Count} need review.";
        });
    }

    [RelayCommand]
    private void Lock()
    {
        _lock.Lock();
        var sp = IPlatformApplication.Current!.Services;
        Application.Current!.Windows[0].Page = sp.GetRequiredService<LockPage>();
    }
}
