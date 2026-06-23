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
        IsBusy      = true;
        ErrorMessage = null;
        try   { await action(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
