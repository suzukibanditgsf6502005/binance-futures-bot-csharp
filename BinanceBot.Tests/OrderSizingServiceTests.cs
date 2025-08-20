using Domain.Trading;
using Infrastructure.Binance.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BinanceBot.Tests;

public class OrderSizingServiceTests
{
    [Fact]
    public void Sizes_qty_ok_with_sufficient_risk()
    {
        var f = new SymbolFilters { Symbol="BTCUSDT", TickSize=0.10m, StepSize=0.001m, MinQty=0.001m, MinNotional=5m };
        var svc = NewSizer(f);
        var ok = svc.TrySize("BTCUSDT", OrderType.Market, entryPrice:113985.80m, stopPrice:113985.80m - 4329.44m, riskUsd:150m,
                             out var qty, out var reason);
        Assert.True(ok);
        Assert.Equal(0.034m, qty);
    }

    [Fact]
    public void Fails_when_risk_below_minQty_requirement()
    {
        var f = new SymbolFilters { Symbol="BTCUSDT", TickSize=0.10m, StepSize=0.001m, MinQty=0.001m, MinNotional=5m };
        var svc = NewSizer(f);
        var ok = svc.TrySize("BTCUSDT", OrderType.Market, entryPrice:30000m, stopPrice:25000m, riskUsd:1m,
                             out var qty, out var reason);
        Assert.False(ok);
        Assert.Contains("Below minQty", reason);
    }

    [Fact]
    public void Bumps_up_to_minNotional_if_within_risk()
    {
        var f = new SymbolFilters { Symbol="ETHUSDT", TickSize=0.10m, StepSize=0.001m, MinQty=0.001m, MinNotional=5m };
        var svc = NewSizer(f);
        var ok = svc.TrySize("ETHUSDT", OrderType.Market, entryPrice:2500m, stopPrice:2450m, riskUsd:10m,
                             out var qty, out var reason);
        Assert.True(ok);
        Assert.True(qty * 2500m >= 5m);
    }

    private static OrderSizingService NewSizer(SymbolFilters f)
    {
        var logger = new NullLogger<OrderSizingService>();
        return new OrderSizingService(logger, s => f);
    }
}
