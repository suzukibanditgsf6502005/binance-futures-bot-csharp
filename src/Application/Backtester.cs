using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Skender.Stock.Indicators;

namespace Application;

public record BacktestResult(int Trades, decimal WinRate, decimal AvgRr, decimal MaxDrawdownPct, IReadOnlyList<decimal> Equity);

public class Backtester
{
    private readonly IStrategy _strategy;
    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public Backtester(IStrategy strategy, AppSettings settings, HttpClient http)
    {
        _strategy = strategy;
        _settings = settings;
        _http = http;
    }

    public async Task RunAsync(string symbol, DateTime from, DateTime to, string interval)
    {
        var klines = await DownloadKlinesAsync(symbol, from, to, interval);
        var result = Run(klines);
        Console.WriteLine($"Trades: {result.Trades}");
        Console.WriteLine($"Winrate: {result.WinRate:F2}%");
        Console.WriteLine($"Avg RR: {result.AvgRr:F2}");
        Console.WriteLine($"Max DD: {result.MaxDrawdownPct:F2}%");
        Console.WriteLine($"Equity: {string.Join(", ", result.Equity.Select(e => e.ToString("F2", CultureInfo.InvariantCulture)))}");
    }

    public BacktestResult Run(List<Kline> klines)
    {
        var quotes = new List<Quote>();
        Trade? open = null;
        decimal equity = 1000m;
        var equityCurve = new List<decimal> { equity };
        var trades = new List<(decimal rr, bool win)>();
        decimal peak = equity;
        decimal maxDd = 0m;

        for (int i = 0; i < klines.Count; i++)
        {
            var k = klines[i];
            quotes.Add(new Quote
            {
                Date = DateTime.SpecifyKind(k.OpenTimeUtc, DateTimeKind.Utc),
                Open = k.Open,
                High = k.High,
                Low = k.Low,
                Close = k.Close,
                Volume = k.Volume
            });

            if (quotes.Count < 15)
                continue;

            var atr = quotes.GetAtr(14).Last().Atr ?? 0d;
            var atrDec = (decimal)atr;

            if (open != null)
            {
                if (open.Side == OrderSide.Buy)
                {
                    if (k.Low <= open.Stop)
                    {
                        var rr = -1m;
                        var pnl = equity * _settings.RiskPerTradePct * rr;
                        equity += pnl;
                        trades.Add((rr, false));
                        equityCurve.Add(equity);
                        peak = Math.Max(peak, equity);
                        maxDd = Math.Min(maxDd, (equity - peak) / peak);
                        open = null;
                        goto AfterTrade;
                    }
                    if (k.High >= open.Tp)
                    {
                        var rr = _settings.Rrr;
                        var pnl = equity * _settings.RiskPerTradePct * rr;
                        equity += pnl;
                        trades.Add((rr, true));
                        equityCurve.Add(equity);
                        peak = Math.Max(peak, equity);
                        maxDd = Math.Min(maxDd, (equity - peak) / peak);
                        open = null;
                        goto AfterTrade;
                    }
                }
                else
                {
                    if (k.High >= open.Stop)
                    {
                        var rr = -1m;
                        var pnl = equity * _settings.RiskPerTradePct * rr;
                        equity += pnl;
                        trades.Add((rr, false));
                        equityCurve.Add(equity);
                        peak = Math.Max(peak, equity);
                        maxDd = Math.Min(maxDd, (equity - peak) / peak);
                        open = null;
                        goto AfterTrade;
                    }
                    if (k.Low <= open.Tp)
                    {
                        var rr = _settings.Rrr;
                        var pnl = equity * _settings.RiskPerTradePct * rr;
                        equity += pnl;
                        trades.Add((rr, true));
                        equityCurve.Add(equity);
                        peak = Math.Max(peak, equity);
                        maxDd = Math.Min(maxDd, (equity - peak) / peak);
                        open = null;
                        goto AfterTrade;
                    }
                }
            }

            AfterTrade:
            if (open == null)
            {
                var signal = _strategy.Evaluate(quotes);
                if (signal == TradeSignal.Long)
                {
                    var stopDist = Math.Max(atrDec * _settings.AtrMultiple, 0.001m);
                    open = new Trade(k.Close, k.Close - stopDist, k.Close + stopDist * _settings.Rrr, OrderSide.Buy, stopDist);
                }
                else if (signal == TradeSignal.Short)
                {
                    var stopDist = Math.Max(atrDec * _settings.AtrMultiple, 0.001m);
                    open = new Trade(k.Close, k.Close + stopDist, k.Close - stopDist * _settings.Rrr, OrderSide.Sell, stopDist);
                }
            }
        }

        var winCount = trades.Count(t => t.win);
        var winRate = trades.Count > 0 ? (decimal)winCount / trades.Count * 100m : 0m;
        var avgRr = trades.Count > 0 ? trades.Average(t => t.rr) : 0m;
        var maxDdPct = maxDd * 100m;
        return new BacktestResult(trades.Count, winRate, avgRr, maxDdPct, equityCurve);
    }

    private async Task<List<Kline>> DownloadKlinesAsync(string symbol, DateTime from, DateTime to, string interval)
    {
        var list = new List<Kline>();
        long start = new DateTimeOffset(from).ToUnixTimeMilliseconds();
        long end = new DateTimeOffset(to).ToUnixTimeMilliseconds();
        while (start < end)
        {
            var url = $"/fapi/v1/klines?symbol={symbol.ToUpper()}&interval={interval}&startTime={start}&endTime={end}&limit=1500";
            var res = await _http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.GetArrayLength() == 0) break;
            foreach (var e in arr.EnumerateArray())
            {
                var openTime = DateTimeOffset.FromUnixTimeMilliseconds(e[0].GetInt64()).UtcDateTime;
                var open = decimal.Parse(e[1].GetString()!, CultureInfo.InvariantCulture);
                var high = decimal.Parse(e[2].GetString()!, CultureInfo.InvariantCulture);
                var low = decimal.Parse(e[3].GetString()!, CultureInfo.InvariantCulture);
                var close = decimal.Parse(e[4].GetString()!, CultureInfo.InvariantCulture);
                var vol = decimal.Parse(e[5].GetString()!, CultureInfo.InvariantCulture);
                var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(e[6].GetInt64()).UtcDateTime;
                list.Add(new Kline
                {
                    OpenTimeUtc = openTime,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = vol,
                    CloseTimeUtc = closeTime,
                    IsClosed = true
                });
            }
            start = new DateTimeOffset(list[^1].CloseTimeUtc).ToUnixTimeMilliseconds() + 1;
            await Task.Delay(50);
        }
        return list;
    }
}

public record Trade(decimal Entry, decimal Stop, decimal Tp, OrderSide Side, decimal Risk);

