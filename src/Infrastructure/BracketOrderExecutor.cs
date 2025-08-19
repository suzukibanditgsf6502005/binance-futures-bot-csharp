using Application;
using Serilog;

namespace Infrastructure;

public class BracketOrderExecutor : IOrderExecutor
{
    private readonly IExchangeClient _exchange;
    private readonly AppSettings _settings;
    private readonly bool _dryRun;
    private readonly IAlertService _alerts;

    public BracketOrderExecutor(IExchangeClient exchange, AppSettings settings, BotOptions options, IAlertService alerts)
    {
        _exchange = exchange;
        _settings = settings;
        _dryRun = options.DryRun;
        _alerts = alerts;
    }

    public async Task OpenWithBracketAsync(string symbol, OrderSide side, decimal qty, decimal price, decimal stopDistance, SymbolFilters filters)
    {
        var slPrice = side == OrderSide.Buy ? price - stopDistance : price + stopDistance;
        var tpPrice = side == OrderSide.Buy ? price + stopDistance * _settings.Rrr : price - stopDistance * _settings.Rrr;

        slPrice = filters.ClampPrice(slPrice);
        tpPrice = filters.ClampPrice(tpPrice);

        if (_dryRun)
        {
            Log.Information("{Symbol}: [DRY] OPEN {Side} qty={Qty} price={Price:F2} SL={Sl:F2} TP={Tp:F2}", symbol, side, qty, price, slPrice, tpPrice);
            await _alerts.SendAsync($"{symbol}: [DRY] OPEN {side} qty={qty} price={price:F2} SL={slPrice:F2} TP={tpPrice:F2}");
            return;
        }

        var entry = await _exchange.PlaceMarketAsync(symbol, side, qty);
        if (!entry.Success)
        {
            Log.Error("{Symbol}: entry failed qty={Qty} price={Price:F2} error={Error}", symbol, qty, price, entry.Error);
            await _alerts.SendAsync($"{symbol}: entry failed qty={qty} price={price:F2} error={entry.Error}");
            return;
        }

        var exitSide = side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        await _exchange.PlaceStopMarketCloseAsync(symbol, exitSide, slPrice);
        await _exchange.PlaceTakeProfitMarketCloseAsync(symbol, exitSide, tpPrice);

        Log.Information("{Symbol}: OPEN {Side} qty={Qty} price={Price:F2} SL={Sl:F2} TP={Tp:F2}", symbol, side, qty, price, slPrice, tpPrice);
        await _alerts.SendAsync($"{symbol}: OPEN {side} qty={qty} price={price:F2} SL={slPrice:F2} TP={tpPrice:F2}");
    }

    public async Task FlipCloseAsync(string symbol, PositionSide posSide)
    {
        var sideLabel = posSide == PositionSide.Long ? "LONG" : "SHORT";

        if (_dryRun)
        {
            Log.Information("{Symbol}: flip detected — [DRY] close {Side}", symbol, sideLabel);
            await _alerts.SendAsync($"{symbol}: [DRY] flip close {sideLabel}");
            return;
        }

        Log.Information("{Symbol}: flip detected — closing {Side} at market", symbol, sideLabel);
        await _exchange.ClosePositionMarketAsync(symbol, posSide);
        await _alerts.SendAsync($"{symbol}: flip close {sideLabel}");
    }
}
