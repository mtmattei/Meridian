using Liveline;
using Liveline.Models;
using Meridian.Presentation;
using Meridian.Services;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;

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

        // Consolidated animation timer — 32ms tick (~30fps) for smooth scrolling
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();

        TradeDrawerPanel.CloseRequested += (_, _) => CloseTradeDrawer();

        // Update clip rect when ticker container sizes
        TickerTapeContainer.SizeChanged += (_, _) =>
        {
            TickerClip.Rect = new Windows.Foundation.Rect(
                0, 0, TickerTapeContainer.ActualWidth, TickerTapeContainer.ActualHeight);
        };

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

        // Slower decorative animations (sub-divided from 30fps tick)
        if (_animationFrame % 3 == 0)
            BrailleSpinner.Text = SpinnerGlyphs[(_animationFrame / 3) % SpinnerGlyphs.Length];

        if (_animationFrame % 5 == 0)
            UpdateBraillePulse();

        // Gain pill pulse: obvious breathing animation (opacity 0.45→1.0 + scale)
        var phase = (_animationFrame % 94) / 94.0 * Math.PI * 2;
        GainPill.Opacity = 0.72 + 0.28 * Math.Sin(phase);
        var scale = 1.0 + 0.02 * Math.Sin(phase);
        GainPillScale.ScaleX = scale;
        GainPillScale.ScaleY = scale;

        if (_animationFrame % 7 == 0)
            UpdateBrailleActivity();

        // Pulsing green dot on selected holding
        if (_selectedHoldingDot != null)
            _selectedHoldingDot.Opacity = 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(phase));
    }

    // ── Ticker Tape ───────────────────────────────────────────────────

    private bool _tickerTapeInitialized;
    private bool _useLiveData;
    private double _tickerWidth;
    private bool _tickerPositioned;
    private double _cachedFooterSegmentWidth;

    private void BuildTickerTapeText()
    {
        var tickers = _useLiveData
            ? _finnhub.GetLatestQuotes()
            : (IReadOnlyList<StreamTicker>)_marketData.GetStreamTickers();

        // Build ticker text for dual-TextBlock marquee (A + B leapfrog)
        var segment = new System.Text.StringBuilder();
        foreach (var t in tickers)
            segment.Append($"⣤⣴⣶⣷ {t.Ticker}  {t.Price}  {t.Delta}   │   ");

        var tickerText = segment.ToString();
        TickerTapeTextA.Text = tickerText;
        TickerTapeTextB.Text = tickerText;

        // Force re-measure on next frame
        _tickerWidth = 0;
        _tickerPositioned = false;
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

        // Measure once after layout
        if (_tickerWidth <= 0)
        {
            var w = TickerTapeTextA.ActualWidth;
            if (w > 50) _tickerWidth = w;
            else return;
        }

        // Position B right after A on first valid measurement
        if (!_tickerPositioned)
        {
            _tickerPositioned = true;
            TickerTranslateA.X = 0;
            TickerTranslateB.X = _tickerWidth;
        }

        // Scroll both TextBlocks left
        TickerTranslateA.X -= 1.9;
        TickerTranslateB.X -= 1.9;

        // Leapfrog: when fully off-screen left, jump behind the other
        if (TickerTranslateA.X < -_tickerWidth)
            TickerTranslateA.X = TickerTranslateB.X + _tickerWidth;
        if (TickerTranslateB.X < -_tickerWidth)
            TickerTranslateB.X = TickerTranslateA.X + _tickerWidth;

        // Footer ticker scroll (doubled for 30fps)
        FooterTickerTranslate.X -= 0.58;
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

    // ── Chart (Liveline) ──────────────────────────────────────────────

    private async Task LoadChartAsync(string? ticker)
    {
        try
        {
            _currentChartTicker = ticker;

            var points = string.IsNullOrEmpty(ticker)
                ? await _marketData.GetPortfolioHistoryAsync(CancellationToken.None)
                : await _marketData.GetStockHistoryAsync(ticker, CancellationToken.None);

            if (points.Count == 0) return;

            // Convert to Liveline data points
            var livelineData = new List<LivelinePoint>(points.Count);
            foreach (var p in points)
            {
                var dt = DateTime.TryParse(p.Date, out var parsed)
                    ? new DateTimeOffset(parsed) : DateTimeOffset.Now;
                livelineData.Add(new LivelinePoint(dt, (double)p.Value));
            }

            var lastValue = (double)points[^1].Value;
            var isPositive = points.Count >= 2 && points[^1].Value >= points[0].Value;

            // Set theme color based on gain/loss
            LivelineChart.Theme = new LivelineTheme
            {
                Color = isPositive ? "#2D6A4F" : "#B5342B",
                IsDark = false
            };

            // Push data — Liveline handles smooth lerp animation
            LivelineChart.Data = livelineData;
            LivelineChart.Value = lastValue;
            LivelineChart.IsLoading = false;

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
            LivelineChart.Height = 240; // Restore full height for portfolio mode
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
            LivelineChart.Height = 210;
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
