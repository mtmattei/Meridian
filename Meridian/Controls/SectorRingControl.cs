using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Meridian.Controls;

public sealed class SectorRingControl : SKXamlCanvas
{
    // ── Theme colors ──────────────────────────────────────────────────
    private static readonly SKColor ColorTextPrimary = new(0x1A, 0x1A, 0x2E);
    private static readonly SKColor ColorTextMuted = new(0x8A, 0x8A, 0x8A);

    // ── Dependency properties ─────────────────────────────────────────
    public static readonly DependencyProperty SectorsProperty =
        DependencyProperty.Register(nameof(Sectors), typeof(IList<Sector>),
            typeof(SectorRingControl), new PropertyMetadata(null, OnDataChanged));

    public IList<Sector>? Sectors
    {
        get => (IList<Sector>?)GetValue(SectorsProperty);
        set => SetValue(SectorsProperty, value);
    }

    // ── Hover state ───────────────────────────────────────────────────
    private int _hoveredIndex = -1;
    private readonly List<ArcHitZone> _arcZones = new();
    private readonly List<float> _legendYPositions = new();
    private float _legendX;

    // ── Cached paints ─────────────────────────────────────────────────
    private readonly SKPaint _arcPaint;
    private readonly SKPaint _centerTextPaint;
    private readonly SKPaint _centerSubPaint;
    private readonly SKPaint _swatchPaint;
    private readonly SKPaint _legendNamePaint;
    private readonly SKPaint _legendPctPaint;

    // ── Cached fonts ──────────────────────────────────────────────────
    private readonly SKFont _centerFontDefault;
    private readonly SKFont _centerFontHovered;
    private readonly SKFont _subFontDefault;
    private readonly SKFont _subFontHovered;
    private readonly SKFont _legendNameFontNormal;
    private readonly SKFont _legendNameFontBold;
    private readonly SKFont _legendPctFont;

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SectorRingControl)d).Invalidate();

    public SectorRingControl()
    {
        // Arc paint (color set per segment in paint loop)
        _arcPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        };

        // Center text paints
        _centerTextPaint = new SKPaint { IsAntialias = true, Color = ColorTextPrimary };
        _centerSubPaint = new SKPaint { IsAntialias = true, Color = ColorTextMuted };

        // Legend paints (color/alpha set per row)
        _swatchPaint = new SKPaint { IsAntialias = true };
        _legendNamePaint = new SKPaint { IsAntialias = true, Color = ColorTextPrimary };
        _legendPctPaint = new SKPaint { IsAntialias = true, Color = ColorTextMuted };

        // Fonts
        var outfitSemiBold = SKTypeface.FromFamilyName("Outfit", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        var outfitNormal = SKTypeface.FromFamilyName("Outfit", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        var plexMono = SKTypeface.FromFamilyName("IBM Plex Mono");

        _centerFontDefault = new SKFont(outfitSemiBold, 16);
        _centerFontHovered = new SKFont(outfitSemiBold, 13);
        _subFontDefault = new SKFont(outfitNormal, 9);
        _subFontHovered = new SKFont(outfitNormal, 11);
        _legendNameFontNormal = new SKFont(outfitNormal, 13);
        _legendNameFontBold = new SKFont(outfitSemiBold, 13);
        _legendPctFont = new SKFont(plexMono, 13);

        PaintSurface += OnPaintSurface;
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => { _hoveredIndex = -1; Invalidate(); };
        Height = 200;
    }

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

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

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var sectors = Sectors;
        if (sectors is null || sectors.Count == 0) return;

        var w = e.Info.Width;
        var h = e.Info.Height;
        if (w <= 0 || h <= 0) return;

        var actualWidth = ActualWidth;
        if (actualWidth <= 0) actualWidth = 1;
        var scale = (float)(w / actualWidth);

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

            _arcPaint.Color = color.WithAlpha(alpha);
            _arcPaint.StrokeWidth = sw;

            canvas.DrawArc(rect, startAngle, sweepAngle - 2, false, _arcPaint);

            // Store hit zone (in device-independent coords)
            _arcZones.Add(new ArcHitZone(
                centerX / scale, centerY / scale,
                (outerRadius - strokeWidth) / scale, (outerRadius + strokeWidth / 2) / scale,
                startAngle, sweepAngle));

            startAngle += sweepAngle;
        }

        // Center text — shows hovered sector or default
        var hasHover = _hoveredIndex >= 0 && _hoveredIndex < sectors.Count;
        var centerLabel = hasHover ? sectors[_hoveredIndex].Name : $"{sectors.Count}";
        var centerSub = hasHover ? $"{sectors[_hoveredIndex].Pct:F1}%" : "SECTORS";

        var centerFont = hasHover ? _centerFontHovered : _centerFontDefault;
        canvas.DrawText(centerLabel, centerX, centerY - 4, SKTextAlign.Center, centerFont, _centerTextPaint);

        var subFont = hasHover ? _subFontHovered : _subFontDefault;
        canvas.DrawText(centerSub, centerX, centerY + 12, SKTextAlign.Center, subFont, _centerSubPaint);

        // Legend (right side)
        var legendX = centerX + outerRadius + 36;
        _legendX = legendX / scale;
        var legendY = centerY - (sectors.Count * 24f / 2f);

        for (int i = 0; i < sectors.Count; i++)
        {
            var sector = sectors[i];
            var color = SKColor.Parse(sector.ColorHex);
            var isHovered = i == _hoveredIndex;
            var alpha = (_hoveredIndex >= 0 && !isHovered) ? (byte)90 : (byte)255;

            _swatchPaint.Color = color.WithAlpha(alpha);
            canvas.DrawRoundRect(legendX, legendY - 6, 8, 8, 2, 2, _swatchPaint);

            var nameFont = isHovered ? _legendNameFontBold : _legendNameFontNormal;
            _legendNamePaint.Color = ColorTextPrimary.WithAlpha(alpha);
            canvas.DrawText(sector.Name, legendX + 16, legendY + 2, SKTextAlign.Left, nameFont, _legendNamePaint);

            _legendPctPaint.Color = ColorTextMuted.WithAlpha(alpha);
            canvas.DrawText($"{sector.Pct:F1}%", w - 10, legendY + 2, SKTextAlign.Right, _legendPctFont, _legendPctPaint);

            _legendYPositions.Add(legendY / scale);
            legendY += 24;
        }
    }
}
