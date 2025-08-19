using Microsoft.Extensions.Hosting;
using Skender.Stock.Indicators;

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
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private readonly CancellationTokenSource _cts = new();

    public BotHostedService(IExchangeClient exchange, AppSettings settings, BotOptions options, IStrategy strategy, IRiskManager riskManager, IOrderExecutor orderExecutor)
    {
        _exchange = exchange;
        _settings = settings;
        _dryRun = options.DryRun;
        _strategy = strategy;
        _riskManager = riskManager;
        _orderExecutor = orderExecutor;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Binance Futures bot starting (Testnet={_settings.UseTestnet}) @ {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        if (_dryRun)
            Console.WriteLine("Dry-run mode: orders will not be placed.");

        foreach (var s in _settings.Symbols)
        {
            _symbolMeta[s] = await _exchange.GetSymbolFiltersAsync(s);
            Console.WriteLine($"Loaded filters for {s}: tickSize={_symbolMeta[s].TickSize}, stepSize={_symbolMeta[s].StepSize}");
        }

        foreach (var s in _settings.Symbols)
        {
            await _exchange.SetLeverageAsync(s, _settings.Leverage);
            Console.WriteLine($"Leverage set to x{_settings.Leverage} on {s}");
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
                Console.WriteLine($"[ERROR] {ex.Message}\n{ex}");
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
            Console.WriteLine($"{symbol}: last candle not closed yet — skipping.");
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
        var signal = _strategy.Evaluate(quotes);

        var pos = await _exchange.GetPositionRiskAsync(symbol);
        var hasLong = pos.PositionAmt > 0m;
        var hasShort = pos.PositionAmt < 0m;

        var account = await _exchange.GetAccountBalanceAsync();
        var qty = _riskManager.CalculateQty(account.AvailableBalance, lastAtr, last.Close, _settings, _symbolMeta[symbol]);
        var riskPerTrade = _settings.RiskPerTradePct * account.AvailableBalance;
        var stopDistance = Math.Max(lastAtr * _settings.AtrMultiple, 0.001m);

        if (qty <= 0m)
        {
            Console.WriteLine($"{symbol}: qty too small after filters — risk={riskPerTrade:F2} stop={stopDistance:F2} price={last.Close:F2}");
            return;
        }

        if (!hasLong && !hasShort)
        {
            if (signal == TradeSignal.Long)
            {
                await _orderExecutor.OpenWithBracketAsync(symbol, OrderSide.Buy, qty, last.Close, stopDistance, _symbolMeta[symbol]);
            }
            else if (signal == TradeSignal.Short)
            {
                await _orderExecutor.OpenWithBracketAsync(symbol, OrderSide.Sell, qty, last.Close, stopDistance, _symbolMeta[symbol]);
            }
            else
            {
                Console.WriteLine($"{symbol}: no signal.");
            }
        }
        else
        {
            if (hasLong && signal == TradeSignal.Short)
            {
                await _orderExecutor.FlipCloseAsync(symbol, PositionSide.Long);
            }
            else if (hasShort && signal == TradeSignal.Long)
            {
                await _orderExecutor.FlipCloseAsync(symbol, PositionSide.Short);
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
