using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Meridian.Controls;

public sealed class SectorRingControl : SKXamlCanvas
{
    public static readonly DependencyProperty SectorsProperty =
        DependencyProperty.Register(nameof(Sectors), typeof(IList<Sector>),
            typeof(SectorRingControl), new PropertyMetadata(null, OnDataChanged));

    public IList<Sector>? Sectors
    {
        get => (IList<Sector>?)GetValue(SectorsProperty);
        set => SetValue(SectorsProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SectorRingControl)d).Invalidate();

    public SectorRingControl()
    {
        PaintSurface += OnPaintSurface;
        Height = 200;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var sectors = Sectors;
        if (sectors is null || sectors.Count == 0) return;

        var w = e.Info.Width;
        var h = e.Info.Height;

        // Donut dimensions
        var ringSize = Math.Min(w * 0.45f, h * 0.8f);
        var centerX = ringSize / 2 + 20;
        var centerY = h / 2f;
        var outerRadius = ringSize / 2f;
        var strokeWidth = 22f;
        var rect = new SKRect(
            centerX - outerRadius + strokeWidth / 2,
            centerY - outerRadius + strokeWidth / 2,
            centerX + outerRadius - strokeWidth / 2,
            centerY + outerRadius - strokeWidth / 2);

        // Draw arcs
        float startAngle = -90f;
        foreach (var sector in sectors)
        {
            var sweepAngle = (float)(sector.Pct / 100.0 * 360.0);
            var color = SKColor.Parse(sector.ColorHex);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = color,
                StrokeWidth = strokeWidth,
                StrokeCap = SKStrokeCap.Round,
            };

            canvas.DrawArc(rect, startAngle, sweepAngle - 2, false, paint);
            startAngle += sweepAngle;
        }

        // Center text
        using var centerPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#1A1A2E"),
            TextSize = 16,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Outfit", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
        };
        canvas.DrawText($"{sectors.Count}", centerX, centerY - 4, centerPaint);

        using var subtitlePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#8A8A8A"),
            TextSize = 9,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Outfit"),
        };
        canvas.DrawText("SECTORS", centerX, centerY + 12, subtitlePaint);

        // Legend (right side)
        var legendX = centerX + outerRadius + 30;
        var legendY = centerY - (sectors.Count * 22f / 2f);

        foreach (var sector in sectors)
        {
            var color = SKColor.Parse(sector.ColorHex);

            // Color swatch
            using var swatchPaint = new SKPaint { IsAntialias = true, Color = color };
            canvas.DrawRoundRect(legendX, legendY - 6, 8, 8, 2, 2, swatchPaint);

            // Name
            using var namePaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColor.Parse("#1A1A2E"),
                TextSize = 11,
                Typeface = SKTypeface.FromFamilyName("Outfit"),
            };
            canvas.DrawText(sector.Name, legendX + 16, legendY + 2, namePaint);

            // Percentage
            using var pctPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColor.Parse("#8A8A8A"),
                TextSize = 11,
                TextAlign = SKTextAlign.Right,
                Typeface = SKTypeface.FromFamilyName("IBM Plex Mono"),
            };
            canvas.DrawText($"{sector.Pct:F1}%", w - 10, legendY + 2, pctPaint);

            legendY += 22;
        }
    }
}
