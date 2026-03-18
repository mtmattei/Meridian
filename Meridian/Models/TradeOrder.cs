namespace Meridian.Models;

public record TradeOrder(
    string Ticker,
    string Side,
    int Quantity,
    string OrderType,
    decimal? LimitPrice,
    decimal EstimatedTotal
);
