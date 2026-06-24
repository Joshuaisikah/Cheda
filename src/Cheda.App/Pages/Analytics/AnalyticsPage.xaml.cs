namespace Cheda.App.Pages.Analytics;

public partial class AnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;
    private bool _hasLoaded;

    public AnalyticsPage(AnalyticsViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        await _vm.RefreshAsync();
        if (!_hasLoaded)
        {
            _hasLoaded               = true;
            LoadingOverlay.IsVisible = false;

            ContentLayout.Opacity      = 0;
            ContentLayout.TranslationY = 24;
            await Task.WhenAll(
                ContentLayout.FadeToAsync(1, 400, Easing.CubicOut),
                ContentLayout.TranslateToAsync(0, 0, 350, Easing.CubicOut));
        }
    }
}
