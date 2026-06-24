namespace Cheda.App.Pages.Settings;

public partial class SettingsPage : ContentPage
{
    private bool _hasLoaded;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
