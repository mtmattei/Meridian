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

    private int _hoveredBar = -1;

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((VolumeChartControl)d).Invalidate();

    public VolumeChartControl()
    {
        PaintSurface += OnPaintSurface;
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => { _hoveredBar = -1; Invalidate(); };
        Height = 140;
    }

    private float _barGap;
    private float _paddingLeft;

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (VolumeData == null || _barGap == 0) return;
        var pos = e.GetCurrentPoint(this).Position;
        var scale = ActualWidth > 0 ? ActualWidth : 1;
        var idx = (int)((pos.X - _paddingLeft / (ActualWidth > 0 ? ActualWidth : 1) * ActualWidth) / (ActualWidth / VolumeData.Count));
        idx = Math.Clamp(idx, 0, VolumeData.Count - 1);
        if (idx != _hoveredBar)
        {
            _hoveredBar = idx;
            Invalidate();
        }
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

        _barGap = barGap;
        _paddingLeft = padding.Left;

        // Hover tooltip
        if (_hoveredBar >= 0 && _hoveredBar < data.Count)
        {
            var hx = padding.Left + _hoveredBar * barGap + barWidth / 2;

            // Crosshair vertical line
            using var crossPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse("#C9A96E").WithAlpha(150),
                StrokeWidth = 1,
                PathEffect = SKPathEffect.CreateDash(new[] { 3f, 3f }, 0),
            };
            canvas.DrawLine(hx, padding.Top, hx, padding.Top + chartH, crossPaint);

            // Dot on envelope
            if (_hoveredBar < envelopePoints.Count)
            {
                using var dotPaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#C9A96E") };
                canvas.DrawCircle(envelopePoints[_hoveredBar], 4, dotPaint);
            }

            // Tooltip box
            var vol = data[_hoveredBar].Volume;
            var label = $"{_hoveredBar}:00 · {vol}M";
            using var tipFont = new SKFont(SKTypeface.FromFamilyName("IBM Plex Mono"), 10);
            using var tipBgPaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#1A1A2E") };
            using var tipTextPaint = new SKPaint { IsAntialias = true, Color = SKColors.White };

            var tipW = 90f;
            var tipH = 22f;
            var tipX = Math.Clamp(hx - tipW / 2, 0, w - tipW);
            var tipY = padding.Top - tipH - 4;

            canvas.DrawRoundRect(tipX, tipY, tipW, tipH, 6, 6, tipBgPaint);
            canvas.DrawText(label, tipX + tipW / 2, tipY + 15, SKTextAlign.Center, tipFont, tipTextPaint);
        }
    }
}
