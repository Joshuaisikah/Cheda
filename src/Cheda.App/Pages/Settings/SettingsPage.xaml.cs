namespace Cheda.App.Pages.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;
    private bool _hasLoaded;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Always reload settings from DB — the VM is created before auth so
        // the constructor can't read the encrypted DB reliably.
        _vm.Reload();

        if (!_hasLoaded)
        {
            _hasLoaded = true;
            ContentLayout.Opacity      = 0;
            ContentLayout.TranslationY = 24;
            await Task.WhenAll(
                ContentLayout.FadeToAsync(1, 400, Easing.CubicOut),
                ContentLayout.TranslateToAsync(0, 0, 350, Easing.CubicOut));
        }
    }
}
