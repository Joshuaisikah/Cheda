namespace Cheda.App.Pages.Review;

public partial class ReviewQueuePage : ContentPage
{
    private readonly ReviewQueueViewModel _vm;
    private bool _hasLoaded;

    public ReviewQueuePage(ReviewQueueViewModel vm)
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
