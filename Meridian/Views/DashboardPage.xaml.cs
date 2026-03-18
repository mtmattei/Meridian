using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Meridian.Presentation;
using Meridian.Services;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;

namespace Meridian.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DispatcherTimer _clockTimer;
    private readonly IMarketDataService _marketData;
    private string? _currentChartTicker;

    private static readonly SKColor GainColor = SKColor.Parse("#2D6A4F");
    private static readonly SKColor LossColor = SKColor.Parse("#B5342B");

    public DashboardPage()
    {
        this.InitializeComponent();

        _marketData = App.Services.GetRequiredService<IMarketDataService>();
        DataContext = new DashboardViewModel(_marketData);

        // Live clock
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        Loaded += async (_, _) => await LoadChartAsync(null);
    }

    private async Task LoadChartAsync(string? ticker)
    {
        _currentChartTicker = ticker;

        var points = string.IsNullOrEmpty(ticker)
            ? await _marketData.GetPortfolioHistoryAsync(CancellationToken.None)
            : await _marketData.GetStockHistoryAsync(ticker, CancellationToken.None);

        var values = points.Select(p => (double)p.Value).ToArray();
        var dates = points.Select(p => p.Date).ToArray();

        var isPositive = values.Length >= 2 && values[^1] >= values[0];
        var color = isPositive ? GainColor : LossColor;

        PerformanceChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(color.WithAlpha(30)),
                Stroke = new SolidColorPaint(color, 2.5f),
                GeometrySize = 0,
                LineSmoothness = 0.65,
            }
        };

        PerformanceChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = dates,
                LabelsRotation = 0,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#C4C0B8")),
                SeparatorsPaint = null,
                ShowSeparatorLines = false,
                ForceStepToMin = true,
                MinStep = Math.Max(1, dates.Length / 5),
            }
        };

        PerformanceChart.YAxes = new Axis[]
        {
            new Axis
            {
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#C4C0B8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E8E4DE"), 1),
                Labeler = val => string.IsNullOrEmpty(ticker)
                    ? $"${val / 1000:N0}k"
                    : $"${val:N0}",
            }
        };

        UpdateChartHeader(ticker);
    }

    private async void UpdateChartHeader(string? ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            ChartLabel.Text = "PERFORMANCE";
            StockDetailPanel.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            var watchlist = await _marketData.GetWatchlistAsync(CancellationToken.None);
            var stock = watchlist.FirstOrDefault(s => s.Ticker == ticker);
            if (stock != null)
            {
                ChartLabel.Text = stock.Name.ToUpperInvariant();
                StockPrice.Text = $"${stock.Price:N2}";
                StockDelta.Text = $"{(stock.Pct >= 0 ? "+" : "")}{stock.Pct:N2}%";
                StockDelta.Foreground = stock.Pct >= 0
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2D, 0x6A, 0x4F))
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xB5, 0x34, 0x2B));
            }
            StockDetailPanel.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
        }
    }

    private async void OnHoldingTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is object dc)
        {
            var tickerProp = dc.GetType().GetProperty("Ticker");
            if (tickerProp?.GetValue(dc) is string ticker)
            {
                // Toggle: tap same holding returns to portfolio
                var newTicker = _currentChartTicker == ticker ? null : ticker;
                await LoadChartAsync(newTicker);
            }
        }
    }

    private async void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        await LoadChartAsync(null);
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("ddd, MMM d · hh:mm:ss tt");
    }
}
