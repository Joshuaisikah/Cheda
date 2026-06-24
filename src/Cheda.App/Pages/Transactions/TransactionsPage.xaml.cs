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
            await ContentLayout.FadeToAsync(1, 350, Easing.CubicOut);
        }
    }
}
