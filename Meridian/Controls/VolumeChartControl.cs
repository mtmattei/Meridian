using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Meridian.Controls;

public sealed class VolumeChartControl : SKXamlCanvas
{
    public static readonly DependencyProperty VolumeDataProperty =
        DependencyProperty.Register(nameof(VolumeData), typeof(IList<VolumeBar>),
            typeof(VolumeChartControl), new PropertyMetadata(null, OnDataChanged));

    public IList<VolumeBar>? VolumeData
    {
        get => (IList<VolumeBar>?)GetValue(VolumeDataProperty);
        set => SetValue(VolumeDataProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((VolumeChartControl)d).Invalidate();

    public VolumeChartControl()
    {
        PaintSurface += OnPaintSurface;
        Height = 140;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var data = VolumeData;
        if (data is null || data.Count == 0) return;

        var w = e.Info.Width;
        var h = e.Info.Height;
        var padding = new SKRect(30, 10, 10, 25);
        var chartW = w - padding.Left - padding.Right;
        var chartH = h - padding.Top - padding.Bottom;

        var maxVol = data.Max(d => d.Volume);
        if (maxVol == 0) maxVol = 1;

        var barWidth = chartW / data.Count * 0.6f;
        var barGap = chartW / data.Count;

        // Grid lines at 25%, 50%, 75%
        using var gridPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#E8E4DE"),
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0),
        };

        for (int pct = 25; pct <= 75; pct += 25)
        {
            var y = padding.Top + chartH * (1 - pct / 100f);
            canvas.DrawLine(padding.Left, y, w - padding.Right, y, gridPaint);
        }

        // Market hours zone (9-16 = indices 9-16)
        var marketStart = padding.Left + 9 * barGap;
        var marketEnd = padding.Left + 16 * barGap + barWidth;
        using var zonePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#C9A96E").WithAlpha(12),
        };
        canvas.DrawRect(marketStart, padding.Top, marketEnd - marketStart, chartH, zonePaint);

        // Bars
        var envelopePoints = new List<SKPoint>();

        for (int i = 0; i < data.Count; i++)
        {
            var vol = data[i].Volume;
            var barH = (float)vol / maxVol * chartH;
            var x = padding.Left + i * barGap;
            var y = padding.Top + chartH - barH;

            // Color: gold for high volume (>55M), gray otherwise
            var isHigh = vol > 55;
            var barColor = isHigh
                ? SKColor.Parse("#C9A96E")
                : SKColor.Parse("#C4C0B8");

            using var barPaint = new SKPaint
            {
                IsAntialias = true,
                Color = barColor.WithAlpha(180),
            };

            var barRect = new SKRoundRect(
                new SKRect(x, y, x + barWidth, padding.Top + chartH),
                barWidth / 2, barWidth / 2);
            canvas.DrawRoundRect(barRect, barPaint);

            // Envelope point at bar top
            envelopePoints.Add(new SKPoint(x + barWidth / 2, y));
        }

        // Envelope line (smooth)
        if (envelopePoints.Count >= 2)
        {
            var envelopePath = new SKPath();
            envelopePath.MoveTo(envelopePoints[0]);
            for (int i = 1; i < envelopePoints.Count; i++)
            {
                var prev = envelopePoints[i - 1];
                var curr = envelopePoints[i];
                var cpx = (prev.X + curr.X) / 2;
                envelopePath.CubicTo(cpx, prev.Y, cpx, curr.Y, curr.X, curr.Y);
            }

            using var envPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse("#C9A96E").WithAlpha(120),
                StrokeWidth = 1.5f,
            };
            canvas.DrawPath(envelopePath, envPaint);
        }

        // X-axis labels (every 6 hours)
        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#C4C0B8"),
            TextSize = 9,
            TextAlign = SKTextAlign.Center,
        };

        for (int i = 0; i < data.Count; i += 6)
        {
            var x = padding.Left + i * barGap + barWidth / 2;
            canvas.DrawText($"{i}:00", x, h - 5, labelPaint);
        }
    }
}
