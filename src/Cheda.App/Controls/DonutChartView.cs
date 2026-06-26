using Cheda.App.Pages.Analytics;

namespace Cheda.App.Controls;

public class DonutChartView : GraphicsView
{
    private readonly DonutDrawable _drawable = new();

    public DonutChartView()
    {
        Drawable = _drawable;
    }

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IReadOnlyList<CategoryChartRow>),
        typeof(DonutChartView),
        null,
        propertyChanged: (b, _, n) =>
        {
            var v = (DonutChartView)b;
            v._drawable.Items = n as IReadOnlyList<CategoryChartRow> ?? [];
            v.Invalidate();
        });

    public IReadOnlyList<CategoryChartRow>? Items
    {
        get => (IReadOnlyList<CategoryChartRow>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
}

internal sealed class DonutDrawable : IDrawable
{
    public IReadOnlyList<CategoryChartRow> Items { get; set; } = [];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Items.Count == 0) return;

        float cx     = dirtyRect.Width  / 2f;
        float cy     = dirtyRect.Height / 2f;
        float outerR = Math.Min(cx, cy) - 6f;
        float innerR = outerR * 0.55f;
        float midR   = (outerR + innerR) / 2f;
        float stroke = outerR - innerR;

        canvas.StrokeSize    = stroke;
        canvas.StrokeLineCap = LineCap.Butt;

        float startAngle = -90f;
        foreach (var item in Items)
        {
            var sweep = (float)(item.Pct / 100.0 * 360.0);
            if (sweep < 0.5f) continue;

            canvas.StrokeColor = item.AccentColor;
            canvas.DrawArc(
                cx - midR, cy - midR, midR * 2, midR * 2,
                startAngle, startAngle + sweep,
                clockwise: true, closed: false);

            startAngle += sweep;
        }
    }
}
