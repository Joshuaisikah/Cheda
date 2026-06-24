namespace Cheda.App.Pages.Insights;

public partial class InsightsPage : ContentPage
{
    private bool _hasLoaded;
    private readonly InsightsViewModel _vm;

    public InsightsPage(InsightsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        await _vm.RefreshAsync();
        if (!_hasLoaded)
        {
            _hasLoaded = true;
            LoadingOverlay.IsVisible = false;

            ContentLayout.Opacity      = 0;
            ContentLayout.TranslationY = 24;
            await Task.WhenAll(
                ContentLayout.FadeToAsync(1, 400, Easing.CubicOut),
                ContentLayout.TranslateToAsync(0, 0, 350, Easing.CubicOut));
        }
    }

    protected override bool OnBackButtonPressed() => false;
}
