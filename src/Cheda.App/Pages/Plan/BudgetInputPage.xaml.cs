namespace Cheda.App.Pages.Plan;

public partial class BudgetInputPage : ContentPage
{
    public decimal? Result { get; private set; }

    private readonly TaskCompletionSource<decimal?> _tcs = new();

    public BudgetInputPage(string emoji, string title, string subtitle, decimal? initial = null)
    {
        BackgroundColor = Color.FromArgb("#99000000");
        InitializeComponent();

        EmojiLabel.Text    = emoji;
        TitleLabel.Text    = title;
        SubtitleLabel.Text = subtitle;

        if (initial.HasValue)
            AmountEntry.Text = initial.Value.ToString("F0");
    }

    // Await this after PushModalAsync — completes when the dialog is dismissed.
    public Task<decimal?> WaitForResultAsync() => _tcs.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(() => AmountEntry.Focus());
    }

    private void OnScrimTapped(object? sender, TappedEventArgs e) => Dismiss(null);
    private void OnCancelClicked(object? sender, EventArgs e) => Dismiss(null);

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var text = AmountEntry.Text?.Trim() ?? "";
        if (!decimal.TryParse(text, out var value) || value <= 0)
        {
            ErrorLabel.Text      = "Please enter a valid amount.";
            ErrorLabel.IsVisible = true;
            return;
        }
        Dismiss(value);
    }

    private void Dismiss(decimal? result)
    {
        Result = result;
        _tcs.TrySetResult(result);
        _ = Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Dismiss(null);
        return true;
    }
}
