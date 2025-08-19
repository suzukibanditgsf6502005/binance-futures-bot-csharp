using Application;

namespace Infrastructure;

public class BracketOrderExecutor : IOrderExecutor
{
    private readonly IExchangeClient _exchange;
    private readonly AppSettings _settings;
    private readonly bool _dryRun;

    public BracketOrderExecutor(IExchangeClient exchange, AppSettings settings, BotOptions options)
    {
        _exchange = exchange;
        _settings = settings;
        _dryRun = options.DryRun;
    }

    public async Task OpenWithBracketAsync(string symbol, OrderSide side, decimal qty, decimal price, decimal stopDistance, SymbolFilters filters)
    {
        var slPrice = side == OrderSide.Buy ? price - stopDistance : price + stopDistance;
        var tpPrice = side == OrderSide.Buy ? price + stopDistance * _settings.Rrr : price - stopDistance * _settings.Rrr;

        slPrice = filters.ClampPrice(slPrice);
        tpPrice = filters.ClampPrice(tpPrice);

        if (_dryRun)
        {
            Console.WriteLine($"{symbol}: [DRY] OPEN {side} qty={qty} @~{price:F2} | SL={slPrice:F2} TP={tpPrice:F2}");
            return;
        }

        var entry = await _exchange.PlaceMarketAsync(symbol, side, qty);
        if (!entry.Success)
        {
            Console.WriteLine($"{symbol}: entry failed — {entry.Error}");
            return;
        }

        var exitSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        await _exchange.PlaceStopMarketCloseAsync(symbol, exitSide, slPrice);
        await _exchange.PlaceTakeProfitMarketCloseAsync(symbol, exitSide, tpPrice);

        Console.WriteLine($"{symbol}: OPEN {side} qty={qty} @~{price:F2} | SL={slPrice:F2} TP={tpPrice:F2}");
    }

    public async Task FlipCloseAsync(string symbol, PositionSide posSide)
    {
        var sideLabel = posSide == PositionSide.Long ? "LONG" : "SHORT";

        if (_dryRun)
        {
            Console.WriteLine($"{symbol}: flip detected — [DRY] would close {sideLabel} at market");
            return;
        }

        Console.WriteLine($"{symbol}: flip detected — closing {sideLabel} at market");
        await _exchange.ClosePositionMarketAsync(symbol, posSide);
    }
}
