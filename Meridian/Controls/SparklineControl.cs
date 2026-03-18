using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Meridian.Controls;

public sealed class SparklineControl : SKXamlCanvas
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IList<double>),
            typeof(SparklineControl), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty IsPositiveProperty =
        DependencyProperty.Register(nameof(IsPositive), typeof(bool),
            typeof(SparklineControl), new PropertyMetadata(true, OnDataChanged));

    public IList<double>? Points
    {
        get => (IList<double>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public bool IsPositive
    {
        get => (bool)GetValue(IsPositiveProperty);
        set => SetValue(IsPositiveProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SparklineControl)d).Invalidate();

    public SparklineControl()
    {
        PaintSurface += OnPaintSurface;
        Width = 72;
        Height = 30;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var points = Points;
        if (points is null || points.Count < 2) return;

        var w = e.Info.Width;
        var h = e.Info.Height;
        var padding = 2f;
        var color = IsPositive
            ? SKColor.Parse("#2D6A4F")
            : SKColor.Parse("#B5342B");

        var min = points.Min();
        var max = points.Max();
        var range = max - min;
        if (range == 0) range = 1;

        var path = new SKPath();
        for (int i = 0; i < points.Count; i++)
        {
            var x = padding + (float)i / (points.Count - 1) * (w - padding * 2);
            var y = padding + (float)(h - padding * 2 - (points[i] - min) / range * (h - padding * 2));
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        // Area fill
        var areaPath = new SKPath(path);
        areaPath.LineTo(w - padding, h);
        areaPath.LineTo(padding, h);
        areaPath.Close();

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, h),
                new[] { color.WithAlpha(50), color.WithAlpha(0) },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(areaPath, fillPaint);

        // Stroke
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = 1.5f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, strokePaint);
    }
}
