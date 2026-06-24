namespace Cheda.App.Pages.Lock;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _vm;

    public LockPage(LockViewModel vm)
    {
        InitializeComponent();
        _vm         = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() =>
        await _vm.InitializeAsync();

    // Prevent hardware back button and edge-swipe gesture from dismissing lock
    protected override bool OnBackButtonPressed() => true;
}
