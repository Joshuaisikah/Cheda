namespace Cheda.App.Pages.Lock;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _vm;

    public LockPage(LockViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
        _ = PulseCurrencyAsync();
    }

    protected override async void OnAppearing() =>
        await _vm.InitializeAsync();

    protected override bool OnBackButtonPressed() => true;

    private async Task PulseCurrencyAsync()
    {
        while (!_vm.IsDismissed)
        {
            await LockCurrencyLabel.ScaleTo(1.25, 700, Easing.SinInOut);
            await LockCurrencyLabel.ScaleTo(1.00, 700, Easing.SinInOut);
        }
    }
}
