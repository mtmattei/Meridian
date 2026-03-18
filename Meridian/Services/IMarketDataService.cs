using Meridian.Models;

namespace Meridian.Services;

public interface IMarketDataService
{
	ValueTask<IImmutableList<Stock>> GetWatchlistAsync(CancellationToken ct);
	ValueTask<IImmutableList<Holding>> GetHoldingsAsync(CancellationToken ct);
	ValueTask<IImmutableList<Sector>> GetSectorsAsync(CancellationToken ct);
	ValueTask<IImmutableList<VolumeBar>> GetVolumeAsync(CancellationToken ct);
	ValueTask<IImmutableList<NewsItem>> GetNewsAsync(CancellationToken ct);
	ValueTask<IImmutableList<ChartPoint>> GetPortfolioHistoryAsync(CancellationToken ct);
	ValueTask<IImmutableList<ChartPoint>> GetStockHistoryAsync(string ticker, CancellationToken ct);
	IImmutableList<IndexTicker> GetIndexTickers();
	IImmutableList<StreamTicker> GetStreamTickers();
}
