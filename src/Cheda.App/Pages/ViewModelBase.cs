using CommunityToolkit.Mvvm.ComponentModel;

namespace Cheda.App.Pages;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected async Task RunAsync(Func<Task> action)
    {
        IsBusy       = true;
        ErrorMessage = null;
        try
        {
            // Run all data-loading and computation off the UI thread.
            // MAUI's binding infrastructure safely dispatches PropertyChanged
            // notifications to the main thread on Android.
            await Task.Run(action);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
