using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Meridian.Presentation;
using Meridian.Services;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;
using SkiaSharp;

namespace Meridian.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _animationTimer;
    private readonly IMarketDataService _marketData;
    private readonly FinnhubService _finnhub;
    private string? _currentChartTicker;
    private Border? _currentExpandedPanel;
    private Border? _selectedHoldingBorder;
    private Border? _selectedHoldingDot;
    private int _animationFrame;
    private bool _isDrawerAnimating;

    // Cached braille TextBlock references (avoids visual tree walk every tick)
    private readonly List<TextBlock> _brailleActivityBlocks = new();
    private bool _brailleBlocksCached;

    // ── Theme colors (from resources, not hardcoded) ──
    private static readonly SKColor GainColor = new(0x2D, 0x6A, 0x4F);
    private static readonly SKColor LossColor = new(0xB5, 0x34, 0x2B);
    private static readonly SKColor AxisLabelColor = new(0xC4, 0xC0, 0xB8);
    private static readonly SKColor TooltipTextColor = new(0x1A, 0x1A, 0x2E);
    private static readonly SKColor TooltipDateColor = new(0x9C, 0x98, 0x90);

    // Tooltip font (loaded once from embedded font file)
    private static SKTypeface? _tooltipTypeface;
    private static SKTypeface GetTooltipTypeface()
    {
        if (_tooltipTypeface != null) return _tooltipTypeface;
        try
        {
            var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "IBM_Plex_Mono", "IBMPlexMono-SemiBold.ttf");
            if (File.Exists(fontPath))
                _tooltipTypeface = SKTypeface.FromFile(fontPath);
        }
        catch { /* fallback to default */ }
        return _tooltipTypeface ?? SKTypeface.Default;
    }

    // Hover brushes (lazy from resources)
    private static SolidColorBrush? _hoverBorderBrush;
    private static SolidColorBrush? _defaultBorderBrush;
    private static SolidColorBrush? _hoverBgBrush;
    private static SolidColorBrush? _transparentBg;

    private static SolidColorBrush HoverBorderBrush => _hoverBorderBrush ??= (SolidColorBrush)Application.Current.Resources["MeridianAccentBrush"];
    private static SolidColorBrush DefaultBorderBrush => _defaultBorderBrush ??= (SolidColorBrush)Application.Current.Resources["MeridianBorderBrush"];
    private static SolidColorBrush HoverBgBrush => _hoverBgBrush ??= (SolidColorBrush)Application.Current.Resources["MeridianCardHoverBrush"];
    private static SolidColorBrush TransparentBg => _transparentBg ??= new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    // ── Braille animation constants ──
    private static readonly string[] SpinnerGlyphs = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly string BraillePulsePattern = "⠀⣀⣤⣴⣶⣷⣿⣷⣶⣴⣤⣀⠀⠀⠀⠀⠀⠀";
    private static readonly string[] BrailleActivityGlyphs = ["⠀", "⣀", "⣤", "⣴", "⣶", "⣷", "⣿"];

    public DashboardPage()
    {
        this.InitializeComponent();

        _marketData = App.Services.GetRequiredService<IMarketDataService>();
        DataContext = new DashboardViewModel(_marketData);

        // Finnhub live ticker data — set FINNHUB_API_KEY env var or replace below
        var finnhubKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY") ?? "d6u0709r01qjm9brvoigd6u0709r01qjm9brvoj0";
        _finnhub = new FinnhubService(
            finnhubKey,
            ["AAPL", "NVDA", "MSFT", "GOOGL", "META", "TSLA"]);
        _finnhub.QuotesUpdated += OnLiveQuotesUpdated;

        // Live clock — 1s interval
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        // Consolidated animation timer — 16ms tick (~60fps) for smooth scrolling
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();

        TradeDrawerPanel.CloseRequested += (_, _) => CloseTradeDrawer();

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Card entrance animation: fade in + slide up
            AnimateCardEntrance();

            await LoadChartAsync(null);
            await LoadSkiaControlsAsync();
            _finnhub.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Page load error: {ex.Message}");
        }
    }

    private void AnimateCardEntrance()
    {
        // Chart card entrance: opacity 0→1 + translateY 24→0, 700ms
        ChartCard.Opacity = 0;
        ChartCardTranslate.Y = 24;

        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(700)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            BeginTime = TimeSpan.FromMilliseconds(150)
        };
        Storyboard.SetTarget(fadeIn, ChartCard);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var slideUp = new DoubleAnimation
        {
            From = 24, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(700)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            BeginTime = TimeSpan.FromMilliseconds(150)
        };
        Storyboard.SetTarget(slideUp, ChartCard);
        Storyboard.SetTargetProperty(slideUp, "(UIElement.RenderTransform).(TranslateTransform.Y)");

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Children.Add(slideUp);
        sb.Begin();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Stop();
        _animationTimer.Stop();
        _finnhub.Stop();
        _finnhub.Dispose();
        _brailleActivityBlocks.Clear();
        _brailleBlocksCached = false;
    }

    private async Task LoadSkiaControlsAsync()
    {
        var sectors = await _marketData.GetSectorsAsync(CancellationToken.None);
        SectorRing.Sectors = sectors.ToList();

        var volume = await _marketData.GetVolumeAsync(CancellationToken.None);
        VolumeChart.VolumeData = volume.ToList();

        // Pre-load sparkline data for watchlist rows (deferred to let ItemsRepeater render)
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(200); // Wait for ItemsRepeater to materialize
            await PopulateSparklines();
        });
    }

    private async Task PopulateSparklines()
    {
        try
        {
            var sparklines = new List<Controls.SparklineControl>();
            FindSparklines(this, sparklines);

            foreach (var spark in sparklines)
            {
                var ticker = ExtractTicker(spark.DataContext);
                if (ticker == null) continue;

                var history = await _marketData.GetStockHistoryAsync(ticker, CancellationToken.None);
                if (history.Count == 0) continue;

                // Use last 24 data points for mini chart
                var points = history.TakeLast(24).Select(p => (double)p.Value).ToList();
                spark.Points = points;
                spark.IsPositive = points.Last() >= points.First();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sparkline load error: {ex.Message}");
        }
    }

    private static void FindSparklines(DependencyObject parent, List<Controls.SparklineControl> results)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Controls.SparklineControl sc)
                results.Add(sc);
            else
                FindSparklines(child, results);
        }
    }

    // ── Trade Drawer ──────────────────────────────────────────────────

    private async void OpenTradeDrawer(string ticker)
    {
        if (_isDrawerAnimating) return;

        try
        {
            var watchlist = await _marketData.GetWatchlistAsync(CancellationToken.None);
            var stock = watchlist.FirstOrDefault(s => s.Ticker == ticker);
            if (stock == null) return;

            TradeDrawerPanel.SetStock(stock);

            // Make both elements visible before animating
            DrawerBackdrop.Opacity = 0;
            DrawerBackdrop.Visibility = Visibility.Visible;
            TradeDrawerPanel.Visibility = Visibility.Visible;

            // Set up slide transform for the drawer panel
            var transform = new TranslateTransform { X = 420 };
            TradeDrawerPanel.RenderTransform = transform;

            _isDrawerAnimating = true;

            // Backdrop fade in: 0 -> 1 over 200ms
            var backdropAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(backdropAnim, DrawerBackdrop);
            Storyboard.SetTargetProperty(backdropAnim, "Opacity");

            // Panel slide in: X 420 -> 0 over 350ms with spring-like easing
            var slideAnim = new DoubleAnimation
            {
                From = 420,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 },
            };
            Storyboard.SetTarget(slideAnim, TradeDrawerPanel);
            Storyboard.SetTargetProperty(slideAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");

            var sb = new Storyboard();
            sb.Children.Add(backdropAnim);
            sb.Children.Add(slideAnim);
            sb.Completed += (_, _) => _isDrawerAnimating = false;
            sb.Begin();
        }
        catch (Exception ex)
        {
            _isDrawerAnimating = false;
            System.Diagnostics.Debug.WriteLine($"Trade drawer error: {ex.Message}");
        }
    }

    private void CloseTradeDrawer()
    {
        if (_isDrawerAnimating) return;

        _isDrawerAnimating = true;

        // Ensure the panel has a TranslateTransform for the slide-out
        if (TradeDrawerPanel.RenderTransform is not TranslateTransform)
            TradeDrawerPanel.RenderTransform = new TranslateTransform { X = 0 };

        // Panel slide out: X 0 -> 420 over 300ms
        var slideAnim = new DoubleAnimation
        {
            From = 0,
            To = 420,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(slideAnim, TradeDrawerPanel);
        Storyboard.SetTargetProperty(slideAnim, "(UIElement.RenderTransform).(TranslateTransform.X)");

        // Backdrop fade out: 1 -> 0 over 300ms
        var backdropAnim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(backdropAnim, DrawerBackdrop);
        Storyboard.SetTargetProperty(backdropAnim, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(slideAnim);
        sb.Children.Add(backdropAnim);
        sb.Completed += (_, _) =>
        {
            DrawerBackdrop.Visibility = Visibility.Collapsed;
            TradeDrawerPanel.Visibility = Visibility.Collapsed;
            _isDrawerAnimating = false;
        };
        sb.Begin();
    }

    private void OnDrawerBackdropTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_isDrawerAnimating) CloseTradeDrawer();
    }

    // ── Animation Timer ───────────────────────────────────────────────

    private void OnAnimationTick(object? sender, object e)
    {
        _animationFrame = (_animationFrame + 1) % 100_000;

        // Smooth ticker scroll every frame
        UpdateTickerScroll();

        // Slower decorative animations (sub-divided from 60fps tick)
        if (_animationFrame % 5 == 0)
            BrailleSpinner.Text = SpinnerGlyphs[(_animationFrame / 5) % SpinnerGlyphs.Length];

        if (_animationFrame % 9 == 0)
            UpdateBraillePulse();

        // Gain pill pulse: obvious breathing animation (opacity 0.45→1.0 + scale)
        var phase = (_animationFrame % 188) / 188.0 * Math.PI * 2;
        GainPill.Opacity = 0.72 + 0.28 * Math.Sin(phase);
        var scale = 1.0 + 0.02 * Math.Sin(phase);
        GainPillScale.ScaleX = scale;
        GainPillScale.ScaleY = scale;

        if (_animationFrame % 13 == 0)
            UpdateBrailleActivity();

        // Pulsing green dot on selected holding
        if (_selectedHoldingDot != null)
            _selectedHoldingDot.Opacity = 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(phase));
    }

    // ── Ticker Tape ───────────────────────────────────────────────────

    private bool _tickerTapeInitialized;
    private bool _useLiveData;
    private double _cachedSegmentWidth;
    private double _cachedFooterSegmentWidth;

    private void BuildTickerTapeText()
    {
        var tickers = _useLiveData
            ? _finnhub.GetLatestQuotes()
            : (IReadOnlyList<StreamTicker>)_marketData.GetStreamTickers();

        // Plain text — 50x repeat for ~15min scroll without any reset/loop
        var segment = new System.Text.StringBuilder();
        foreach (var t in tickers)
            segment.Append($"⣤⣴⣶⣷ {t.Ticker}  {t.Price}  {t.Delta}   │   ");

        var oneSegment = segment.ToString();
        var sb = new System.Text.StringBuilder(oneSegment.Length * 50);
        for (int i = 0; i < 50; i++)
            sb.Append(oneSegment);

        TickerTapeText.Text = sb.ToString();
        // Reset scroll position on content rebuild (live data change is natural content swap)
        TickerTranslate.X = 0;

        // Reset cached widths on rebuild (new content may differ)
        _cachedSegmentWidth = 0;
        _cachedFooterSegmentWidth = 0;

        // Also populate footer ticker
        BuildFooterTickerText(tickers);
    }

    private void BuildFooterTickerText(IReadOnlyList<StreamTicker> tickers)
    {
        var sb = new System.Text.StringBuilder();
        for (int repeat = 0; repeat < 5; repeat++)
        {
            foreach (var t in tickers)
                sb.Append($"⣤⣴⣶⣷ {t.Ticker} {t.Price}  ·  ");
        }
        FooterTickerText.Text = sb.ToString();
    }

    private void InitTickerTape()
    {
        if (_tickerTapeInitialized) return;
        _tickerTapeInitialized = true;
        BuildTickerTapeText();
    }

    private void OnLiveQuotesUpdated()
    {
        // Called from Finnhub when new quotes arrive — update ticker tape on UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            _useLiveData = true;
            BuildTickerTapeText();
        });
    }

    private void UpdateTickerScroll()
    {
        InitTickerTape();
        // Continuous scroll — no reset, no loop. 50x buffer lasts ~15 minutes.
        // Live data updates rebuild the text and reset position naturally.
        TickerTranslate.X -= 0.95;

        // Footer ticker scroll (slower pace, same no-reset approach)
        FooterTickerTranslate.X -= 0.29;
    }

    private int _pulseOffset;
    private void UpdateBraillePulse()
    {
        _pulseOffset = (_pulseOffset + 1) % BraillePulsePattern.Length;
        BraillePulse.Text = BraillePulsePattern[_pulseOffset..] + BraillePulsePattern[.._pulseOffset];
    }

    // ── Braille Activity (cached references) ──────────────────────────

    private int _brailleActivityFrame;

    private void CacheBrailleBlocks()
    {
        if (_brailleBlocksCached) return;
        _brailleBlocksCached = true;
        _brailleActivityBlocks.Clear();
        FindTaggedBlocks(this, "BrailleActivity", _brailleActivityBlocks);
    }

    private static void FindTaggedBlocks(DependencyObject parent, string tag, List<TextBlock> results)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock tb && tb.Tag as string == tag)
                results.Add(tb);
            else
                FindTaggedBlocks(child, tag, results);
        }
    }

    private void UpdateBrailleActivity()
    {
        CacheBrailleBlocks();
        _brailleActivityFrame = (_brailleActivityFrame + 1) % 100;

        var sb = new System.Text.StringBuilder(6);
        for (int i = 0; i < 6; i++)
        {
            var phase = (_brailleActivityFrame + i * 2) % (BrailleActivityGlyphs.Length * 2);
            if (phase >= BrailleActivityGlyphs.Length)
                phase = BrailleActivityGlyphs.Length * 2 - 1 - phase;
            sb.Append(BrailleActivityGlyphs[Math.Clamp(phase, 0, BrailleActivityGlyphs.Length - 1)]);
        }
        var text = sb.ToString();

        foreach (var tb in _brailleActivityBlocks)
            tb.Text = text;
    }

    // ── Chart ─────────────────────────────────────────────────────────

    private async Task LoadChartAsync(string? ticker)
    {
        try
        {
            _currentChartTicker = ticker;

            var points = string.IsNullOrEmpty(ticker)
                ? await _marketData.GetPortfolioHistoryAsync(CancellationToken.None)
                : await _marketData.GetStockHistoryAsync(ticker, CancellationToken.None);

            if (points.Count == 0) return;

            var values = points.Select(p => (double)p.Value).ToArray();
            var dates = points.Select(p => p.Date).ToArray();

            var isPositive = values.Length >= 2 && values[^1] >= values[0];
            var color = isPositive ? GainColor : LossColor;
            var axisLabelPaint = new SolidColorPaint(AxisLabelColor);

            // Gradient fill under the line chart (not the container)
            var gradientFill = new LiveChartsCore.SkiaSharpView.Painting.LinearGradientPaint(
                new[] { color.WithAlpha(60), color.WithAlpha(5) },
                new SKPoint(0, 0), new SKPoint(0, 1));

            PerformanceChart.AnimationsSpeed = TimeSpan.FromMilliseconds(800);
            PerformanceChart.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = values,
                    Fill = gradientFill,
                    Stroke = new SolidColorPaint(color, 2.5f),
                    GeometrySize = 0,
                    LineSmoothness = 0.65,
                    YToolTipLabelFormatter = p =>
                    {
                        var idx = (int)p.Index;
                        var date = idx < dates.Length && DateTime.TryParse(dates[idx], out var dt)
                            ? dt.ToString("MMM d, yyyy") : "";
                        var value = string.IsNullOrEmpty(ticker)
                            ? $"${p.Model / 1000:N1}k"
                            : $"${p.Model:N2}";
                        return $"{date}\n{value}";
                    },
                }
            };

            PerformanceChart.TooltipBackgroundPaint = new SolidColorPaint(SKColors.White);
            PerformanceChart.TooltipTextPaint = new SolidColorPaint(TooltipTextColor)
            {
                SKTypeface = GetTooltipTypeface()
            };
            PerformanceChart.TooltipTextSize = 13;

            // Parse dates for X axis labeling
            var parsedDates = dates.Select(d =>
                DateTime.TryParse(d, out var dt) ? dt : DateTime.MinValue).ToArray();

            PerformanceChart.XAxes = new Axis[]
            {
                new Axis
                {
                    LabelsRotation = 0,
                    TextSize = 10,
                    LabelsPaint = axisLabelPaint,
                    SeparatorsPaint = null,
                    ShowSeparatorLines = false,
                    MinStep = 1,
                    Labeler = val =>
                    {
                        var idx = (int)Math.Round(val);
                        if (idx < 0 || idx >= parsedDates.Length) return "";
                        // Show label only at evenly spaced intervals (~6 labels)
                        var step = Math.Max(1, parsedDates.Length / 6);
                        if (idx % step != 0 && idx != parsedDates.Length - 1) return "";
                        var dt = parsedDates[idx];
                        return dt == DateTime.MinValue ? "" : dt.ToString("MMM d");
                    },
                }
            };

            var yMin = values.Min();
            var yMax = values.Max();
            var yRange = yMax - yMin;
            var yPad = yRange * 0.02;
            var yStep = yRange > 0 ? yRange / 4.0 : 1;

            PerformanceChart.YAxes = new Axis[]
            {
                new Axis
                {
                    TextSize = 10,
                    LabelsPaint = axisLabelPaint,
                    SeparatorsPaint = null,
                    ShowSeparatorLines = false,
                    MinLimit = yMin - yPad,
                    MaxLimit = yMax + yPad,
                    MinStep = yStep,
                    ForceStepToMin = true,
                    Labeler = val => string.IsNullOrEmpty(ticker)
                        ? $"${val / 1000:N0}k"
                        : $"${val:N0}",
                }
            };

            UpdateChartHeader(ticker);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chart load error: {ex.Message}");
        }
    }

    private async void UpdateChartHeader(string? ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            ChartLabel.Text = "PERFORMANCE";
            StockDetailPanel.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
            PerformanceChart.Height = 240; // Restore full height for portfolio mode
            return;
        }

        try
        {
            var watchlist = await _marketData.GetWatchlistAsync(CancellationToken.None);
            var stock = watchlist.FirstOrDefault(s => s.Ticker == ticker);
            if (stock == null) return;

            ChartLabel.Text = stock.Name.ToUpperInvariant();
            StockPrice.Text = $"${stock.Price:N2}";
            StockDelta.Text = $"{(stock.Pct >= 0 ? "+" : "")}{stock.Pct:N2}%";
            StockDelta.Foreground = stock.Pct >= 0
                ? (SolidColorBrush)Application.Current.Resources["MeridianGainBrush"]
                : (SolidColorBrush)Application.Current.Resources["MeridianLossBrush"];

            StockDetailPanel.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;

            // Reduce chart height for stock detail mode (210px vs 240px)
            PerformanceChart.Height = 210;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Chart header error: {ex.Message}");
        }
    }

    // ── Holdings tap (no reflection — use dynamic) ────────────────────

    private static string? ExtractTicker(object dataContext)
    {
        // MVUX generated bindable wraps the record; dynamic avoids reflection overhead
        try { return ((dynamic)dataContext).Ticker as string; }
        catch { return null; }
    }

    private async void OnHoldingTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border tappedBorder) return;
        if (tappedBorder.DataContext is not { } dc) return;

        var ticker = ExtractTicker(dc);
        if (ticker == null) return;

        var isDeselecting = _currentChartTicker == ticker;

        // Clear previous selection
        if (_selectedHoldingBorder != null)
        {
            _selectedHoldingBorder.BorderBrush = DefaultBorderBrush;
            _selectedHoldingBorder.Background = TransparentBg;
        }
        if (_selectedHoldingDot != null)
            _selectedHoldingDot.Visibility = Visibility.Collapsed;

        if (isDeselecting)
        {
            _selectedHoldingBorder = null;
            _selectedHoldingDot = null;
            await LoadChartAsync(null);
        }
        else
        {
            // Highlight new selection
            _selectedHoldingBorder = tappedBorder;
            tappedBorder.BorderBrush = HoverBorderBrush;
            tappedBorder.Background = HoverBgBrush;

            // Find and show the ActiveDot
            _selectedHoldingDot = FindActiveDot(tappedBorder);
            if (_selectedHoldingDot != null)
                _selectedHoldingDot.Visibility = Visibility.Visible;

            await LoadChartAsync(ticker);
        }
    }

    private static Border? FindActiveDot(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border b && b.Tag as string == "ActiveDot")
                return b;
            var found = FindActiveDot(child);
            if (found != null) return found;
        }
        return null;
    }

    private async void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        // Clear holding selection
        if (_selectedHoldingBorder != null)
        {
            _selectedHoldingBorder.BorderBrush = DefaultBorderBrush;
            _selectedHoldingBorder.Background = TransparentBg;
            _selectedHoldingBorder = null;
        }
        if (_selectedHoldingDot != null)
        {
            _selectedHoldingDot.Visibility = Visibility.Collapsed;
            _selectedHoldingDot = null;
        }
        await LoadChartAsync(null);
    }

    // ── Watchlist expand/collapse ─────────────────────────────────────

    private void OnWatchlistRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement tappedRow) return;
        if (tappedRow.Parent is not StackPanel parent) return;

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

        if (_currentExpandedPanel != null && _currentExpandedPanel != expandedPanel)
            _currentExpandedPanel.Visibility = Visibility.Collapsed;

        var isExpanding = expandedPanel.Visibility != Visibility.Visible;
        expandedPanel.Visibility = isExpanding ? Visibility.Visible : Visibility.Collapsed;
        _currentExpandedPanel = isExpanding ? expandedPanel : null;

        // Invalidate braille cache when watchlist layout changes
        _brailleBlocksCached = false;
    }

    private async void OnViewChartFromWatchlist(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            var parent = fe;
            while (parent != null)
            {
                var ticker = ExtractTicker(parent.DataContext);
                if (ticker != null)
                {
                    await LoadChartAsync(ticker);
                    return;
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
        e.Handled = true;
        if (sender is FrameworkElement fe)
        {
            var parent = fe;
            while (parent != null)
            {
                var ticker = ExtractTicker(parent.DataContext);
                if (ticker != null)
                {
                    OpenTradeDrawer(ticker);
                    return;
                }
                parent = parent.Parent as FrameworkElement;
            }
        }
    }

    // ── Hover effects ─────────────────────────────────────────────────

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.BorderBrush = HoverBorderBrush;
            b.Background = HoverBgBrush;
        }
    }

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            // Don't clear highlight on the selected holding
            if (b == _selectedHoldingBorder) return;
            b.BorderBrush = DefaultBorderBrush;
            b.Background = TransparentBg;
        }
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Background = HoverBgBrush;
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Background = TransparentBg;
    }

    // News item hover slide (translateX 4px per spec)
    private void OnNewsItemPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.RenderTransform = new TranslateTransform { X = 4 };
    }

    private void OnNewsItemPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.RenderTransform = null;
    }

    private void OnChartCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
            b.BorderBrush = HoverBorderBrush;
    }

    private void OnChartCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
            b.BorderBrush = DefaultBorderBrush;
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("ddd, MMM d · hh:mm:ss tt");
    }
}
