using Microsoft.Extensions.Hosting;
using Skender.Stock.Indicators;
using Serilog;

namespace Application;

public class BotHostedService : IHostedService
{
    private readonly IExchangeClient _exchange;
    private readonly AppSettings _settings;
    private readonly bool _dryRun;
    private readonly IStrategy _strategy;
    private readonly IRiskManager _riskManager;
    private readonly IOrderExecutor _orderExecutor;
    private readonly Dictionary<string, SymbolFilters> _symbolMeta = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAlertService _alerts;
    private readonly Dictionary<string, TradeState> _trades = new(StringComparer.OrdinalIgnoreCase);
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private readonly CancellationTokenSource _cts = new();

    public BotHostedService(IExchangeClient exchange, AppSettings settings, BotOptions options, IStrategy strategy, IRiskManager riskManager, IOrderExecutor orderExecutor, IAlertService alerts)
    {
        _exchange = exchange;
        _settings = settings;
        _dryRun = options.DryRun;
        _strategy = strategy;
        _riskManager = riskManager;
        _orderExecutor = orderExecutor;
        _alerts = alerts;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Log.Information("Binance Futures bot starting (Testnet={Testnet})", _settings.UseTestnet);
        if (_dryRun)
            Log.Information("Dry-run mode: orders will not be placed.");

        foreach (var s in _settings.Symbols)
        {
            _symbolMeta[s] = await _exchange.GetSymbolFiltersAsync(s);
            Log.Information("Loaded filters for {Symbol}: tickSize={Tick} stepSize={Step}", s, _symbolMeta[s].TickSize, _symbolMeta[s].StepSize);
        }

        foreach (var s in _settings.Symbols)
        {
            await _exchange.SetLeverageAsync(s, _settings.Leverage);
            Log.Information("Leverage set to x{Leverage} on {Symbol}", _settings.Leverage, s);
        }

        _timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        _executingTask = Task.Run(() => ExecuteAsync(_cts.Token));
    }

    private async Task ExecuteAsync(CancellationToken token)
    {
        while (_timer != null && await _timer.WaitForNextTickAsync(token))
        {
            try
            {
                foreach (var symbol in _settings.Symbols)
                {
                    await RunSymbolAsync(symbol);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Execution error");
                await _alerts.SendAsync($"Execution error: {ex.Message}");
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
            return;

        _cts.Cancel();
        _timer?.Dispose();
        await _executingTask;
    }

    private async Task RunSymbolAsync(string symbol)
    {
        var klines = await _exchange.GetKlinesAsync(symbol, _settings.Interval, 500);
        if (klines.Count == 0) return;

        if (!klines[^1].IsClosed)
        {
            Log.Information("{Symbol}: last candle not closed yet — skipping", symbol);
            return;
        }

        IEnumerable<Quote> quotes = klines.Select(k => new Quote
        {
            Date = DateTime.SpecifyKind(k.OpenTimeUtc, DateTimeKind.Utc),
            Open = k.Open,
            High = k.High,
            Low = k.Low,
            Close = k.Close,
            Volume = k.Volume
        });

        var atr = quotes.GetAtr(14).ToList();
        if (atr.Count < 5) return;

        var last = klines[^1];
        var lastAtr = (decimal)(atr[^1].Atr ?? 0d);
        var atrSeries = atr.Where(a => a.Atr.HasValue).Select(a => (decimal)a.Atr!.Value).ToList();
        var atrPercentile = EntryFilters.PercentileRank(atrSeries, lastAtr);
        var signal = _strategy.Evaluate(quotes);
        Log.Information("{Symbol}: signal {Signal} @ {Price}", symbol, signal, last.Close);

        var pos = await _exchange.GetPositionRiskAsync(symbol);
        var hasLong = pos.PositionAmt > 0m;
        var hasShort = pos.PositionAmt < 0m;

        if (!hasLong && !hasShort && _trades.TryGetValue(symbol, out var closed))
        {
            if (closed.Side == OrderSide.Buy)
            {
                if (last.Close <= closed.Stop)
                    await _alerts.SendAsync($"{symbol}: SL hit at {last.Close:F2}");
                else if (last.Close >= closed.Tp)
                    await _alerts.SendAsync($"{symbol}: TP hit at {last.Close:F2}");
            }
            else
            {
                if (last.Close >= closed.Stop)
                    await _alerts.SendAsync($"{symbol}: SL hit at {last.Close:F2}");
                else if (last.Close <= closed.Tp)
                    await _alerts.SendAsync($"{symbol}: TP hit at {last.Close:F2}");
            }
            _trades.Remove(symbol);
        }

        if ((hasLong || hasShort) && _trades.TryGetValue(symbol, out var active))
        {
            var changed = active.Update(last.Close, lastAtr, _settings.BreakEvenAtRr, _settings.AtrTrailMultiple, _symbolMeta[symbol]);
            if (changed)
            {
                var exitSide = active.Side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
                if (!_dryRun)
                    await _exchange.PlaceStopMarketCloseAsync(symbol, exitSide, active.Stop);
                Log.Information("{Symbol}: SL updated to {Sl:F2}", symbol, active.Stop);
                await _alerts.SendAsync($"{symbol}: SL updated to {active.Stop:F2}");
            }
        }

        if (!hasLong && !hasShort)
        {
            if (EntryFilters.IsFundingBlackout(DateTimeOffset.UtcNow, _settings.FundingBlackoutMinutes))
            {
                Log.Information("{Symbol}: within funding blackout window — skipping", symbol);
                return;
            }

            if (atrPercentile < _settings.AtrPercentileMin || atrPercentile > _settings.AtrPercentileMax)
            {
                Log.Information("{Symbol}: ATR percentile {Pct:F2} outside {Min}-{Max} — skipping", symbol, atrPercentile, _settings.AtrPercentileMin, _settings.AtrPercentileMax);
                return;
            }

            var account = await _exchange.GetAccountBalanceAsync();
            var qty = _riskManager.CalculateQty(account.AvailableBalance, lastAtr, last.Close, _settings, _symbolMeta[symbol]);
            var riskPerTrade = _settings.RiskPerTradePct * account.AvailableBalance;
            var stopDistance = Math.Max(lastAtr * _settings.AtrMultiple, 0.001m);

            if (qty <= 0m)
            {
                Log.Information("{Symbol}: qty too small after filters risk={Risk:F2} stop={Stop:F2} price={Price:F2}", symbol, riskPerTrade, stopDistance, last.Close);
                return;
            }

            if (signal == TradeSignal.Long)
            {
                await _orderExecutor.OpenWithBracketAsync(symbol, OrderSide.Buy, qty, last.Close, stopDistance, _symbolMeta[symbol]);
                var sl = _symbolMeta[symbol].ClampPrice(last.Close - stopDistance);
                var tp = _symbolMeta[symbol].ClampPrice(last.Close + stopDistance * _settings.Rrr);
                _trades[symbol] = new TradeState(OrderSide.Buy, last.Close, sl, tp);
            }
            else if (signal == TradeSignal.Short)
            {
                await _orderExecutor.OpenWithBracketAsync(symbol, OrderSide.Sell, qty, last.Close, stopDistance, _symbolMeta[symbol]);
                var sl = _symbolMeta[symbol].ClampPrice(last.Close + stopDistance);
                var tp = _symbolMeta[symbol].ClampPrice(last.Close - stopDistance * _settings.Rrr);
                _trades[symbol] = new TradeState(OrderSide.Sell, last.Close, sl, tp);
            }
            else
            {
                Log.Information("{Symbol}: no signal", symbol);
            }
        }
        else
        {
            if (hasLong && signal == TradeSignal.Short)
            {
                await _orderExecutor.FlipCloseAsync(symbol, PositionSide.Long);
                _trades.Remove(symbol);
            }
            else if (hasShort && signal == TradeSignal.Long)
            {
                await _orderExecutor.FlipCloseAsync(symbol, PositionSide.Short);
                _trades.Remove(symbol);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer?.Dispose();
        _cts.Dispose();
    }
}
