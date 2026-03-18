namespace Meridian.Models;

public record Stock(
    string Ticker,
    string Name,
    decimal Price,
    decimal Change,
    decimal Pct,
    decimal High,
    decimal Low,
    decimal Open,
    string Volume
);
