namespace Cheda.App.Pages.Review;

public partial class ReviewQueuePage : ContentPage
{
    private readonly ReviewQueueViewModel _vm;

    public ReviewQueuePage(ReviewQueueViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing() =>
        await _vm.RefreshAsync();
}
