using System;

namespace Infrastructure.Binance.Models;

public sealed class SymbolFilters
{
    public string Symbol { get; init; } = default!;
    public decimal TickSize { get; init; }
    public decimal StepSize { get; init; }
    public decimal? MinQty { get; init; }
    public decimal? MarketMinQty { get; init; }
    public decimal? MinNotional { get; init; }

    public decimal ClampQuantity(decimal qty)
    {
        if (qty <= 0 || StepSize <= 0) return 0;
        return Math.Floor(qty / StepSize) * StepSize;
    }

    public decimal ClampPrice(decimal price)
    {
        return Math.Round(Math.Floor(price / TickSize) * TickSize, 8);
    }
}
