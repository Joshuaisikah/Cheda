namespace Cheda.App.Pages.Dashboard;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;
    private bool _hasLoaded;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        if (!_hasLoaded)
        {
            _hasLoaded             = true;
            ContentLayout.Opacity      = 0;
            ContentLayout.TranslationY = 24;
            _ = PulseCurrencyAsync();
            await _vm.RefreshAsync();
            await Task.WhenAll(
                LoadingOverlay.FadeToAsync(0, 320, Easing.CubicIn),
                ContentLayout.FadeToAsync(1, 400, Easing.CubicOut),
                ContentLayout.TranslateToAsync(0, 0, 350, Easing.CubicOut));
            LoadingOverlay.IsVisible = false;
        }
        else
        {
            await _vm.RefreshAsync();
        }
    }

    private async Task PulseCurrencyAsync()
    {
        while (LoadingOverlay.IsVisible)
        {
            await CurrencyLabel.ScaleTo(1.25, 700, Easing.SinInOut);
            await CurrencyLabel.ScaleTo(1.00, 700, Easing.SinInOut);
        }
    }
}
