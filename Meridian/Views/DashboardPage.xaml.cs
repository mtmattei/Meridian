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
    private readonly DispatcherTimer _animationTimer;
    private readonly IMarketDataService _marketData;
    private string? _currentChartTicker;
    private Border? _currentExpandedPanel;
    private int _animationFrame;

    private static readonly SKColor GainColor = SKColor.Parse("#2D6A4F");
    private static readonly SKColor LossColor = SKColor.Parse("#B5342B");

    // Braille spinner glyphs (80ms cycle ≈ every tick of 70ms timer)
    private static readonly string[] SpinnerGlyphs = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    // Braille ticker tape segments for scrolling effect
    private static readonly string[] BrailleWaveChars = ["⠀", "⣀", "⣤", "⣴", "⣶", "⣷", "⣿", "⣷", "⣶", "⣴", "⣤", "⣀"];

    public DashboardPage()
    {
        this.InitializeComponent();

        _marketData = App.Services.GetRequiredService<IMarketDataService>();
        DataContext = new DashboardViewModel(_marketData);

        // Live clock — 1s interval
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        // Consolidated animation timer — 70ms tick drives braille spinner + ticker tape
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();

        // Trade drawer close event
        TradeDrawerPanel.CloseRequested += (_, _) => CloseTradeDrawer();

        Loaded += async (_, _) =>
        {
            await LoadChartAsync(null);
            await LoadSkiaControlsAsync();
        };
    }

    private async Task LoadSkiaControlsAsync()
    {
        // Load sector + volume data imperatively (MVUX feed binding doesn't reach SKXamlCanvas DPs)
        var sectors = await _marketData.GetSectorsAsync(CancellationToken.None);
        SectorRing.Sectors = sectors.ToList();

        var volume = await _marketData.GetVolumeAsync(CancellationToken.None);
        VolumeChart.VolumeData = volume.ToList();
    }

    private async void OpenTradeDrawer(string ticker)
    {
        var watchlist = await _marketData.GetWatchlistAsync(CancellationToken.None);
        var stock = watchlist.FirstOrDefault(s => s.Ticker == ticker);
        if (stock == null) return;

        TradeDrawerPanel.SetStock(stock);
        DrawerBackdrop.Visibility = Visibility.Visible;
        TradeDrawerPanel.Visibility = Visibility.Visible;
    }

    private void CloseTradeDrawer()
    {
        DrawerBackdrop.Visibility = Visibility.Collapsed;
        TradeDrawerPanel.Visibility = Visibility.Collapsed;
    }

    private void OnDrawerBackdropTapped(object sender, TappedRoutedEventArgs e)
    {
        CloseTradeDrawer();
    }

    private void OnAnimationTick(object? sender, object e)
    {
        _animationFrame++;

        // Braille spinner: cycle every tick (~70ms ≈ 80ms spec)
        BrailleSpinner.Text = SpinnerGlyphs[_animationFrame % SpinnerGlyphs.Length];

        // Ticker tape scroll: continuous pixel scroll
        UpdateTickerScroll();

        // Braille pulse: shift every 2nd tick (~140ms)
        if (_animationFrame % 2 == 0)
        {
            UpdateBraillePulse();
        }

        // Gain pill subtle pulse: 3s cycle (≈43 ticks)
        var pulsePhase = (_animationFrame % 43) / 43.0;
        var pulseOpacity = 0.85 + 0.15 * Math.Sin(pulsePhase * Math.PI * 2);
        GainPill.Opacity = pulseOpacity;

        // Animate braille activity dots in watchlist rows (every 3rd tick ≈ 210ms)
        if (_animationFrame % 3 == 0)
        {
            UpdateBrailleActivity();
        }
    }

    private bool _tickerTapeInitialized;

    private void InitTickerTape()
    {
        if (_tickerTapeInitialized) return;
        _tickerTapeInitialized = true;

        // Build a doubled string for seamless looping
        var tickers = _marketData.GetStreamTickers();
        var sb = new System.Text.StringBuilder();
        // Build segment twice for seamless loop
        for (int repeat = 0; repeat < 2; repeat++)
        {
            foreach (var t in tickers)
            {
                sb.Append("⣤⣴⣶⣷ ");
                sb.Append($"{t.Ticker} {t.Price} {t.Delta}  │  ");
            }
        }
        TickerTapeText.Text = sb.ToString();
    }

    private void UpdateTickerScroll()
    {
        InitTickerTape();

        // Scroll 2px per tick (70ms → ~28px/sec)
        TickerTranslate.X -= 2;

        // Reset when half the text has scrolled out (seamless loop)
        if (TickerTranslate.X < -1400)
            TickerTranslate.X = 0;
    }

    // Braille pulse for Market Pulse header
    private static readonly string BraillePulsePattern = "⠀⣀⣤⣴⣶⣷⣿⣷⣶⣴⣤⣀⠀⠀⠀⠀⠀⠀";
    private int _pulseOffset;

    private void UpdateBraillePulse()
    {
        _pulseOffset = (_pulseOffset + 1) % BraillePulsePattern.Length;
        var shifted = BraillePulsePattern[_pulseOffset..] + BraillePulsePattern[.._pulseOffset];
        BraillePulse.Text = shifted;
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
                YToolTipLabelFormatter = p =>
                {
                    var idx = (int)p.Index;
                    var date = idx < dates.Length && DateTime.TryParse(dates[idx], out var dt)
                        ? dt.ToString("MMM d, yyyy") : "";
                    return string.IsNullOrEmpty(ticker)
                        ? $"{date}\n${p.Model / 1000:N1}k"
                        : $"{date}\n${p.Model:N2}";
                },
            }
        };

        // Tooltip styling: smaller, white background
        PerformanceChart.TooltipBackgroundPaint = new SolidColorPaint(SKColors.White);
        PerformanceChart.TooltipTextPaint = new SolidColorPaint(SKColor.Parse("#1A1A2E"));
        PerformanceChart.TooltipTextSize = 11;

        // Format X axis: show formatted date labels with proper step
        var formattedDates = dates.Select(d =>
            DateTime.TryParse(d, out var dt) ? dt.ToString("MMM d") : d).ToArray();

        PerformanceChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = formattedDates,
                LabelsRotation = 0,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#C4C0B8")),
                SeparatorsPaint = null,
                ShowSeparatorLines = false,
                ForceStepToMin = true,
                MinStep = Math.Max(1, dates.Length / 6),
            }
        };

        PerformanceChart.YAxes = new Axis[]
        {
            new Axis
            {
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#C4C0B8")),
                SeparatorsPaint = null,
                ShowSeparatorLines = false,
                MinLimit = null, // Let it auto-scale to fill
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

    private void OnWatchlistRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement tappedRow) return;

        // Find the parent StackPanel that contains both the row and the expanded panel
        var parent = tappedRow.Parent as StackPanel;
        if (parent is null) return;

        // Find the expanded panel (tagged "ExpandedPanel")
        Border? expandedPanel = null;
        foreach (var child in parent.Children)
        {
            if (child is Border b && b.Tag as string == "ExpandedPanel")
            {
                expandedPanel = b;
                break;
            }
        }

        if (expandedPanel is null) return;

        // Collapse previously expanded panel
        if (_currentExpandedPanel != null && _currentExpandedPanel != expandedPanel)
        {
            _currentExpandedPanel.Visibility = Visibility.Collapsed;
        }

        // Toggle current panel
        if (expandedPanel.Visibility == Visibility.Visible)
        {
            expandedPanel.Visibility = Visibility.Collapsed;
            _currentExpandedPanel = null;
        }
        else
        {
            expandedPanel.Visibility = Visibility.Visible;
            _currentExpandedPanel = expandedPanel;
        }
    }

    private async void OnViewChartFromWatchlist(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            // Walk up to find the DataContext with Ticker
            var parent = fe;
            while (parent != null)
            {
                if (parent.DataContext is object dc)
                {
                    var tickerProp = dc.GetType().GetProperty("Ticker");
                    if (tickerProp?.GetValue(dc) is string ticker && !string.IsNullOrEmpty(ticker))
                    {
                        await LoadChartAsync(ticker);
                        return;
                    }
                }
                parent = parent.Parent as FrameworkElement;
            }
        }
    }

    private void OnChartTradeButtonClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentChartTicker))
            OpenTradeDrawer(_currentChartTicker);
    }

    private void OnWatchlistTradeButtonTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true; // Prevent row tap from firing
        if (sender is FrameworkElement fe)
        {
            var parent = fe;
            while (parent != null)
            {
                if (parent.DataContext is object dc)
                {
                    var tickerProp = dc.GetType().GetProperty("Ticker");
                    if (tickerProp?.GetValue(dc) is string ticker && !string.IsNullOrEmpty(ticker))
                    {
                        OpenTradeDrawer(ticker);
                        return;
                    }
                }
                parent = parent.Parent as FrameworkElement;
            }
        }
    }

    // ── Hover effects ──

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush HoverBorderBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xC9, 0xA9, 0x6E));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DefaultBorderBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xE4, 0xDE));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush HoverBgBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xFA, 0xF8, 0xF5));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush TransparentBg =
        new(Microsoft.UI.Colors.Transparent);

    private void OnCardPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = HoverBorderBrush;
            // Use RenderTransform instead of Translation to avoid layout jitter
            b.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = -2 };
        }
    }

    private void OnCardPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = DefaultBorderBrush;
            b.RenderTransform = null;
        }
    }

    private void OnRowPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Background = HoverBgBrush;
    }

    private void OnRowPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Background = TransparentBg;
    }

    // Braille activity animation for watchlist rows
    private static readonly string[] BrailleActivityGlyphs = ["⠀", "⣀", "⣤", "⣴", "⣶", "⣷", "⣿"];
    private int _brailleActivityFrame;

    private void UpdateBrailleActivity()
    {
        _brailleActivityFrame++;
        // Build a 6-char oscillating pattern with phase offsets
        var sb = new System.Text.StringBuilder(6);
        for (int i = 0; i < 6; i++)
        {
            var phase = (_brailleActivityFrame + i * 2) % (BrailleActivityGlyphs.Length * 2);
            if (phase >= BrailleActivityGlyphs.Length)
                phase = BrailleActivityGlyphs.Length * 2 - 1 - phase;
            sb.Append(BrailleActivityGlyphs[Math.Clamp(phase, 0, BrailleActivityGlyphs.Length - 1)]);
        }
        var newText = sb.ToString();

        // Walk visual tree to find all TextBlocks tagged "BrailleActivity"
        FindAndUpdateTagged(this, "BrailleActivity", newText);
    }

    private static void FindAndUpdateTagged(DependencyObject parent, string tag, string text)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock tb && tb.Tag as string == tag)
            {
                tb.Text = text;
            }
            else
            {
                FindAndUpdateTagged(child, tag, text);
            }
        }
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("ddd, MMM d · hh:mm:ss tt");
    }
}
