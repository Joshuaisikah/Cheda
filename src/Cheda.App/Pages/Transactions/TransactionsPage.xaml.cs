namespace Cheda.App.Pages.Transactions;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _vm;
    private bool _hasLoaded;

    public TransactionsPage(TransactionsViewModel vm)
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
            ContentLayout.TranslationY = 24;
            await Task.WhenAll(
                ContentLayout.FadeToAsync(1, 400, Easing.CubicOut),
                ContentLayout.TranslateToAsync(0, 0, 350, Easing.CubicOut));
        }
    }
}
