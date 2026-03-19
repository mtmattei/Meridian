using System.Net.Http;
using System.Text.Json;
using Meridian.Models;

namespace Meridian.Services;

/// <summary>
/// Fetches real-time stock quotes from Finnhub API.
/// Free tier: 60 calls/minute. We poll 6 tickers every 15s = 24 calls/min.
/// </summary>
public sealed class FinnhubService : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string[] _tickers;
    private readonly DispatcherTimer _pollTimer;
    private readonly Dictionary<string, StreamTicker> _latestQuotes = new();
    private readonly object _lock = new();

    public event Action? QuotesUpdated;

    public FinnhubService(string apiKey, string[] tickers)
    {
        _apiKey = apiKey;
        _tickers = tickers;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://finnhub.io/api/v1/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Initialize with placeholder data
        foreach (var t in _tickers)
            _latestQuotes[t] = new StreamTicker(t, "$0.00", "0.00%", true);

        // Poll every 15 seconds (4 calls/min per ticker × 6 tickers = 24/min, well under 60 limit)
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _pollTimer.Tick += async (_, _) => await PollAllAsync();
    }

    public void Start()
    {
        _pollTimer.Start();
        // Fire initial poll immediately
        _ = PollAllAsync();
    }

    public void Stop() => _pollTimer.Stop();

    public IReadOnlyList<StreamTicker> GetLatestQuotes()
    {
        lock (_lock)
        {
            return _tickers
                .Where(t => _latestQuotes.ContainsKey(t))
                .Select(t => _latestQuotes[t])
                .ToList();
        }
    }

    private async Task PollAllAsync()
    {
        foreach (var ticker in _tickers)
        {
            try
            {
                var response = await _http.GetAsync($"quote?symbol={ticker}&token={_apiKey}");
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var price = root.GetProperty("c").GetDecimal();
                var change = root.GetProperty("dp").GetDecimal();

                if (price == 0) continue; // Skip invalid/empty responses

                var isUp = change >= 0;
                var streamTicker = new StreamTicker(
                    ticker,
                    $"${price:N2}",
                    $"{(isUp ? "+" : "")}{change:N2}%",
                    isUp);

                lock (_lock)
                {
                    _latestQuotes[ticker] = streamTicker;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Finnhub error for {ticker}: {ex.Message}");
            }
        }

        QuotesUpdated?.Invoke();
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _http.Dispose();
    }
}
