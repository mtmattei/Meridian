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

    private int _hoveredIndex = -1;
    private readonly List<ArcHitZone> _arcZones = new();

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SectorRingControl)d).Invalidate();

    public SectorRingControl()
    {
        PaintSurface += OnPaintSurface;
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => { _hoveredIndex = -1; Invalidate(); };
        Height = 200;
    }

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;
        var scaleX = _arcZones.Count > 0 ? ActualWidth : 1;
        var scaleY = _arcZones.Count > 0 ? ActualHeight : 1;

        var newHovered = -1;
        for (int i = 0; i < _arcZones.Count; i++)
        {
            var z = _arcZones[i];
            var dx = pos.X - z.CenterX;
            var dy = pos.Y - z.CenterY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < z.OuterRadius && dist > z.InnerRadius)
            {
                var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                if (angle < -90) angle += 360;
                if (angle >= z.StartAngle && angle < z.StartAngle + z.SweepAngle)
                {
                    newHovered = i;
                    break;
                }
            }
        }

        // Also check legend rows
        if (newHovered < 0)
        {
            for (int i = 0; i < _legendYPositions.Count; i++)
            {
                var ly = _legendYPositions[i];
                if (pos.Y >= ly - 10 && pos.Y <= ly + 10 && pos.X > _legendX)
                {
                    newHovered = i;
                    break;
                }
            }
        }

        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            Invalidate();
        }
    }

    private record struct ArcHitZone(float CenterX, float CenterY, float InnerRadius, float OuterRadius, float StartAngle, float SweepAngle);
    private readonly List<float> _legendYPositions = new();
    private float _legendX;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var sectors = Sectors;
        if (sectors is null || sectors.Count == 0) return;

        var w = e.Info.Width;
        var h = e.Info.Height;
        var scale = (float)(w / ActualWidth);

        // Donut dimensions
        var ringSize = Math.Min(w * 0.42f, h * 0.8f);
        var centerX = ringSize / 2 + 20;
        var centerY = h / 2f;
        var outerRadius = ringSize / 2f;
        var strokeWidth = 22f;

        _arcZones.Clear();
        _legendYPositions.Clear();

        // Draw arcs
        float startAngle = -90f;
        for (int i = 0; i < sectors.Count; i++)
        {
            var sector = sectors[i];
            var sweepAngle = (float)(sector.Pct / 100.0 * 360.0);
            var color = SKColor.Parse(sector.ColorHex);
            var isHovered = i == _hoveredIndex;

            var expand = isHovered ? 4f : 0f;
            var sw = isHovered ? strokeWidth + 6 : strokeWidth;
            var alpha = (_hoveredIndex >= 0 && !isHovered) ? (byte)90 : (byte)255;

            var rect = new SKRect(
                centerX - outerRadius - expand + sw / 2,
                centerY - outerRadius - expand + sw / 2,
                centerX + outerRadius + expand - sw / 2,
                centerY + outerRadius + expand - sw / 2);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = color.WithAlpha(alpha),
                StrokeWidth = sw,
                StrokeCap = SKStrokeCap.Round,
            };

            canvas.DrawArc(rect, startAngle, sweepAngle - 2, false, paint);

            // Store hit zone (in device-independent coords)
            _arcZones.Add(new ArcHitZone(
                centerX / scale, centerY / scale,
                (outerRadius - strokeWidth) / scale, (outerRadius + strokeWidth / 2) / scale,
                startAngle, sweepAngle));

            startAngle += sweepAngle;
        }

        // Center text — shows hovered sector or default
        var centerLabel = _hoveredIndex >= 0 ? sectors[_hoveredIndex].Name : $"{sectors.Count}";
        var centerSub = _hoveredIndex >= 0 ? $"{sectors[_hoveredIndex].Pct:F1}%" : "SECTORS";

        using var centerFont = new SKFont(SKTypeface.FromFamilyName("Outfit", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), _hoveredIndex >= 0 ? 13 : 16);
        using var centerPaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#1A1A2E") };
        canvas.DrawText(centerLabel, centerX, centerY - 4, SKTextAlign.Center, centerFont, centerPaint);

        using var subFont = new SKFont(SKTypeface.FromFamilyName("Outfit"), _hoveredIndex >= 0 ? 11 : 9);
        using var subPaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#8A8A8A") };
        canvas.DrawText(centerSub, centerX, centerY + 12, SKTextAlign.Center, subFont, subPaint);

        // Legend (right side)
        var legendX = centerX + outerRadius + 36;
        _legendX = legendX / scale;
        var legendY = centerY - (sectors.Count * 22f / 2f);

        for (int i = 0; i < sectors.Count; i++)
        {
            var sector = sectors[i];
            var color = SKColor.Parse(sector.ColorHex);
            var isHovered = i == _hoveredIndex;
            var alpha = (_hoveredIndex >= 0 && !isHovered) ? (byte)90 : (byte)255;

            using var swatchPaint = new SKPaint { IsAntialias = true, Color = color.WithAlpha(alpha) };
            canvas.DrawRoundRect(legendX, legendY - 6, 8, 8, 2, 2, swatchPaint);

            using var nameFont = new SKFont(SKTypeface.FromFamilyName("Outfit", isHovered ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 11);
            using var namePaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#1A1A2E").WithAlpha(alpha) };
            canvas.DrawText(sector.Name, legendX + 16, legendY + 2, SKTextAlign.Left, nameFont, namePaint);

            using var pctFont = new SKFont(SKTypeface.FromFamilyName("IBM Plex Mono"), 11);
            using var pctPaint = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#8A8A8A").WithAlpha(alpha) };
            canvas.DrawText($"{sector.Pct:F1}%", w - 10, legendY + 2, SKTextAlign.Right, pctFont, pctPaint);

            _legendYPositions.Add(legendY / scale);
            legendY += 22;
        }
    }
}
