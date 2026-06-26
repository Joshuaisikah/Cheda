using Microsoft.Maui.Graphics;

namespace Cheda.App.Controls;

internal sealed class MatrixRainDrawable : IDrawable
{
    private static readonly string GlyphSet =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノ0123456789ΩΨΦβ░▒│∑∏√∂∞";

    private readonly Random _rng = new();
    private Drop[] _drops;
    private const float CellH = 17f;

    private struct Drop
    {
        public float X, Y, Speed;
        public int Len;
        public char[] Glyphs;
    }

    public MatrixRainDrawable(int columns = 18)
    {
        _drops = new Drop[columns];
        for (int i = 0; i < columns; i++)
            Seed(ref _drops[i], i, columns, scattered: true);
    }

    private void Seed(ref Drop d, int index, int total, bool scattered)
    {
        d.X      = (index + 0.5f) / total;
        d.Y      = scattered ? -(float)_rng.NextDouble() * 2f : -0.15f - (float)_rng.NextDouble() * 0.4f;
        d.Speed  = 0.003f + (float)_rng.NextDouble() * 0.009f;
        d.Len    = 5 + _rng.Next(13);
        d.Glyphs = new char[20];
        for (int k = 0; k < 20; k++)
            d.Glyphs[k] = GlyphSet[_rng.Next(GlyphSet.Length)];
    }

    public void Tick()
    {
        for (int i = 0; i < _drops.Length; i++)
        {
            _drops[i].Y += _drops[i].Speed;
            if (_drops[i].Y > 1.35f)
                Seed(ref _drops[i], i, _drops.Length, scattered: false);
            if (_rng.Next(4) == 0)
                _drops[i].Glyphs[_rng.Next(20)] = GlyphSet[_rng.Next(GlyphSet.Length)];
        }
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.Font     = new Microsoft.Maui.Graphics.Font("monospace");
        canvas.FontSize = 13f;

        for (int i = 0; i < _drops.Length; i++)
        {
            float x     = _drops[i].X * rect.Width;
            float headY = _drops[i].Y * rect.Height;
            int   len   = _drops[i].Len;

            for (int j = 0; j < len; j++)
            {
                float y = headY - j * CellH;
                if (y < -CellH || y > rect.Height) continue;

                if (j == 0)
                {
                    canvas.FontColor = Color.FromRgba(200, 255, 210, 1f);
                }
                else
                {
                    float fade = MathF.Max(0f, 1f - (float)j / len * 1.5f);
                    canvas.FontColor = Color.FromRgba(0, 200, 50, fade * 0.45f);
                }

                int gi = (j + (int)(headY / CellH)) & 19;
                canvas.DrawString(_drops[i].Glyphs[gi].ToString(), x, y, HorizontalAlignment.Center);
            }
        }
    }
}
