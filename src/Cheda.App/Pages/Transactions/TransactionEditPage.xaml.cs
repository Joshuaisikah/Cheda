namespace Cheda.App.Pages.Transactions;

public partial class TransactionEditPage : ContentPage
{
    public TransactionEditPage(TransactionEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
