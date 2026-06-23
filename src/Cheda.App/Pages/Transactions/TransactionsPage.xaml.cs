namespace Cheda.App.Pages.Transactions;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _vm;

    public TransactionsPage(TransactionsViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() =>
        await _vm.RefreshAsync();
}
