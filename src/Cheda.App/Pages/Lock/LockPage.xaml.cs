using Cheda.App.Controls;

namespace Cheda.App.Pages.Lock;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel       _vm;
    private readonly MatrixRainDrawable  _rain = new(20);
    private CancellationTokenSource?     _cts;

    public LockPage(LockViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _cts             = new CancellationTokenSource();
        MatrixView.Drawable = _rain;

        _ = RunRainAsync(_cts.Token);
        _ = TypeQuoteAsync(_cts.Token);
        _ = PulseCurrencyAsync();

        await _vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    protected override bool OnBackButtonPressed() => true;

    // ── Matrix rain loop (50fps) ─────────────────────────────────────────
    private async Task RunRainAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                _rain.Tick();
                MatrixView.Invalidate();
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── "Neo, wake up..." typewriter ─────────────────────────────────────
    private async Task TypeQuoteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(800, ct);
            await TypeLineAsync(MatrixLine1, "> Wake up, Neo...",              58, ct);
            await Task.Delay(480, ct);
            await TypeLineAsync(MatrixLine2, "> The Matrix has you...",         52, ct);
            await Task.Delay(480, ct);
            await TypeLineAsync(MatrixLine3, "> Follow the white rabbit.  \U0001F407", 44, ct);
        }
        catch (OperationCanceledException) { }
    }

    private async Task TypeLineAsync(Label label, string text, int msPerChar, CancellationToken ct)
    {
        label.Opacity = 1;
        label.Text    = "";
        foreach (char c in text)
        {
            if (ct.IsCancellationRequested) return;
            label.Text += c;
            await Task.Delay(msPerChar, ct);
        }
        // Blink cursor 3 times then leave clean
        _ = BlinkAsync(label, ct);
    }

    private static async Task BlinkAsync(Label label, CancellationToken ct)
    {
        try
        {
            string full = label.Text;
            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(350, ct);
                label.Text = i % 2 == 0 ? full + "_" : full;
            }
            label.Text = full;
        }
        catch (OperationCanceledException) { }
    }

    // ── Pulsing ₵ in the spinner ─────────────────────────────────────────
    private async Task PulseCurrencyAsync()
    {
        while (!_vm.IsDismissed)
        {
            await LockCurrencyLabel.ScaleTo(1.35, 650, Easing.SinInOut);
            await LockCurrencyLabel.ScaleTo(1.00, 650, Easing.SinInOut);
        }
    }
}
