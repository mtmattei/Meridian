using Meridian.Models;

namespace Meridian.Services;

public class MockMarketDataService : IMarketDataService
{
	private readonly IImmutableList<Stock> _stocks;
	private readonly IImmutableList<Holding> _holdings;
	private readonly IImmutableList<Sector> _sectors;
	private readonly IImmutableList<VolumeBar> _volume;
	private readonly IImmutableList<NewsItem> _news;
	private readonly IImmutableList<ChartPoint> _portfolioHistory;
	private readonly Dictionary<string, IImmutableList<ChartPoint>> _stockHistories;
	private readonly IImmutableList<IndexTicker> _indexTickers;
	private readonly IImmutableList<StreamTicker> _streamTickers;

	public MockMarketDataService()
	{
		_stocks = BuildStocks();
		_holdings = BuildHoldings();
		_sectors = BuildSectors();
		_volume = BuildVolume();
		_news = BuildNews();
		_portfolioHistory = BuildPortfolioHistory();
		_stockHistories = BuildStockHistories();
		_indexTickers = BuildIndexTickers();
		_streamTickers = BuildStreamTickers();
	}

	public ValueTask<IImmutableList<Stock>> GetWatchlistAsync(CancellationToken ct) =>
		ValueTask.FromResult(_stocks);

	public ValueTask<IImmutableList<Holding>> GetHoldingsAsync(CancellationToken ct) =>
		ValueTask.FromResult(_holdings);

	public ValueTask<IImmutableList<Sector>> GetSectorsAsync(CancellationToken ct) =>
		ValueTask.FromResult(_sectors);

	public ValueTask<IImmutableList<VolumeBar>> GetVolumeAsync(CancellationToken ct) =>
		ValueTask.FromResult(_volume);

	public ValueTask<IImmutableList<NewsItem>> GetNewsAsync(CancellationToken ct) =>
		ValueTask.FromResult(_news);

	public ValueTask<IImmutableList<ChartPoint>> GetPortfolioHistoryAsync(CancellationToken ct) =>
		ValueTask.FromResult(_portfolioHistory);

	public ValueTask<IImmutableList<ChartPoint>> GetStockHistoryAsync(string ticker, CancellationToken ct) =>
		ValueTask.FromResult(
			_stockHistories.TryGetValue(ticker, out var history)
				? history
				: ImmutableList<ChartPoint>.Empty as IImmutableList<ChartPoint>);

	public IImmutableList<IndexTicker> GetIndexTickers() => _indexTickers;

	public IImmutableList<StreamTicker> GetStreamTickers() => _streamTickers;

	// ── Stock data ──────────────────────────────────────────────────────

	private static IImmutableList<Stock> BuildStocks() =>
		ImmutableList.Create(
			new Stock("AAPL", "Apple Inc.", 247.63m, 3.42m, 1.40m, 251.20m, 244.18m, 244.88m, "62.1M"),
			new Stock("NVDA", "NVIDIA Corp.", 892.14m, -12.82m, -1.42m, 901.44m, 886.22m, 898.10m, "41.3M"),
			new Stock("MSFT", "Microsoft Corp.", 468.21m, 5.18m, 1.12m, 472.80m, 464.15m, 465.00m, "28.7M"),
			new Stock("GOOGL", "Alphabet Inc.", 142.58m, -1.24m, -0.86m, 145.20m, 141.80m, 143.50m, "22.4M"),
			new Stock("META", "Meta Platforms", 350.42m, 8.56m, 2.50m, 355.00m, 345.20m, 346.00m, "35.8M"),
			new Stock("JPM", "JPMorgan Chase", 195.84m, 2.14m, 1.10m, 198.40m, 194.20m, 194.50m, "14.2M"),
			new Stock("AMZN", "Amazon.com", 178.32m, -2.44m, -1.35m, 182.10m, 177.50m, 180.00m, "52.6M"),
			new Stock("TSLA", "Tesla Inc.", 248.92m, 12.48m, 5.28m, 252.80m, 238.40m, 240.00m, "89.4M"));

	// ── Holdings data ───────────────────────────────────────────────────

	private static IImmutableList<Holding> BuildHoldings() =>
		ImmutableList.Create(
			new Holding("AAPL", 85, 178.40m, 247.63m),
			new Holding("NVDA", 22, 480.00m, 892.14m),
			new Holding("MSFT", 40, 380.00m, 468.21m),
			new Holding("GOOGL", 60, 142.58m, 142.58m),
			new Holding("META", 18, 350.42m, 350.42m),
			new Holding("JPM", 30, 195.84m, 195.84m),
			new Holding("TSLA", 15, 210.00m, 248.92m));

	// ── Sectors data ────────────────────────────────────────────────────

	private static IImmutableList<Sector> BuildSectors() =>
		ImmutableList.Create(
			new Sector("Technology", 68.2, "#2D6A4F"),
			new Sector("Consumer Disc.", 14.5, "#C9A96E"),
			new Sector("Financials", 9.8, "#8A8A8A"),
			new Sector("Healthcare", 4.8, "#B5342B"),
			new Sector("Energy", 2.7, "#C4C0B8"));

	// ── Volume data (24 hourly bars) ────────────────────────────────────

	private static IImmutableList<VolumeBar> BuildVolume()
	{
		// Realistic intraday volume (millions): low overnight, spike at open, taper off
		int[] volumes =
		[
			5, 5, 5, 4, 6, 7, 9, 12, 16,
			45, 82, 68, 55, 62, 58, 72, 65,
			48, 32, 18, 13, 8, 6, 5
		];

		var builder = ImmutableList.CreateBuilder<VolumeBar>();
		for (var h = 0; h < 24; h++)
		{
			builder.Add(new VolumeBar($"{h}:00", volumes[h]));
		}
		return builder.ToImmutable();
	}

	// ── News data ───────────────────────────────────────────────────────

	private static IImmutableList<NewsItem> BuildNews() =>
		ImmutableList.Create(
			new NewsItem(
				"2m",
				"Fed signals potential rate adjustment in Q2 as inflation data shows mixed signals across key economic indicators",
				"Macro"),
			new NewsItem(
				"18m",
				"NVIDIA beats earnings expectations, raises guidance on strong data center demand and AI infrastructure growth",
				"Earnings"),
			new NewsItem(
				"34m",
				"Treasury yields climb as inflation data exceeds forecasts, pushing 10-year note to highest level since November",
				"Bonds"),
			new NewsItem(
				"1h",
				"Apple announces expanded AI features across product lineup, including enhanced Siri capabilities and on-device processing",
				"Tech"));

	// ── Portfolio history (90 days) ─────────────────────────────────────

	private static IImmutableList<ChartPoint> BuildPortfolioHistory()
	{
		const int days = 90;
		const double startValue = 123_500.0;
		const double endValue = 163_842.0;
		var startDate = new DateTimeOffset(2025, 12, 18, 0, 0, 0, TimeSpan.Zero);

		return GenerateRandomWalk(startDate, days, startValue, endValue, seed: 42);
	}

	// ── Per-stock histories (90 days each) ──────────────────────────────

	private static Dictionary<string, IImmutableList<ChartPoint>> BuildStockHistories()
	{
		const int days = 90;
		var startDate = new DateTimeOffset(2025, 12, 18, 0, 0, 0, TimeSpan.Zero);

		// (ticker, endPrice, approximateStartPrice, seed)
		(string Ticker, double End, double Start, int Seed)[] specs =
		[
			("AAPL", 247.63, 218.00, 100),
			("NVDA", 892.14, 820.00, 200),
			("MSFT", 468.21, 430.00, 300),
			("GOOGL", 142.58, 138.00, 400),
			("META", 350.42, 310.00, 500),
			("JPM", 195.84, 182.00, 600),
			("AMZN", 178.32, 170.00, 700),
			("TSLA", 248.92, 210.00, 800),
		];

		var dict = new Dictionary<string, IImmutableList<ChartPoint>>(specs.Length);
		foreach (var (ticker, end, start, seed) in specs)
		{
			dict[ticker] = GenerateRandomWalk(startDate, days, start, end, seed);
		}
		return dict;
	}

	// ── Index tickers ───────────────────────────────────────────────────

	private static IImmutableList<IndexTicker> BuildIndexTickers() =>
		ImmutableList.Create(
			new IndexTicker("S&P 500", "5,892", "+0.87%", true),
			new IndexTicker("NASDAQ", "18,742", "+1.12%", true),
			new IndexTicker("DOW 30", "43,218", "+0.34%", true));

	// ── Stream tickers ──────────────────────────────────────────────────

	private static IImmutableList<StreamTicker> BuildStreamTickers() =>
		ImmutableList.Create(
			new StreamTicker("AAPL", "$247.63", "+1.40%", true),
			new StreamTicker("NVDA", "$892.14", "-1.42%", false),
			new StreamTicker("MSFT", "$468.21", "+1.12%", true),
			new StreamTicker("GOOGL", "$142.58", "-0.86%", false),
			new StreamTicker("META", "$350.42", "+2.50%", true),
			new StreamTicker("TSLA", "$248.92", "+5.28%", true));

	// ── Helper: seeded random walk that hits exact start/end values ─────

	private static IImmutableList<ChartPoint> GenerateRandomWalk(
		DateTimeOffset startDate, int days, double startValue, double endValue, int seed)
	{
		var rng = new Random(seed);
		var builder = ImmutableList.CreateBuilder<ChartPoint>();

		// Generate raw random walk
		var raw = new double[days];
		raw[0] = 0.0;
		for (var i = 1; i < days; i++)
		{
			// Daily volatility ~1.2%
			var dailyReturn = (rng.NextDouble() - 0.48) * 0.024;
			raw[i] = raw[i - 1] + dailyReturn;
		}

		// Scale so that raw[0] → startValue and raw[days-1] → endValue
		var rawRange = raw[days - 1] - raw[0];
		var targetRange = endValue - startValue;

		for (var i = 0; i < days; i++)
		{
			double value;
			if (Math.Abs(rawRange) < 1e-10)
			{
				// Flat walk — linearly interpolate
				value = startValue + (targetRange * i / (days - 1));
			}
			else
			{
				var fraction = (raw[i] - raw[0]) / rawRange;
				value = startValue + fraction * targetRange;
			}

			var date = startDate.AddDays(i);
			builder.Add(new ChartPoint(date.ToString("yyyy-MM-dd"), (decimal)Math.Round(value, 2)));
		}

		return builder.ToImmutable();
	}
}
