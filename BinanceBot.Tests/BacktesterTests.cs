using System;
using System.Collections.Generic;
using Application;
using Skender.Stock.Indicators;
using System.Linq;
using System.Net.Http;
using Xunit;

public class SequenceStrategy : IStrategy
{
    private readonly Dictionary<int, TradeSignal> _signals;
    public SequenceStrategy(Dictionary<int, TradeSignal> signals)
    {
        _signals = signals;
    }
    public TradeSignal Evaluate(IEnumerable<Quote> quotes)
    {
        var idx = quotes.Count() - 1;
        if (_signals.TryGetValue(idx, out var s))
            return s;
        return TradeSignal.None;
    }
}

public class BacktesterTests
{
    [Fact]
    public void Backtester_ComputesMetrics()
    {
        var klines = new List<Kline>();
        var start = DateTime.UtcNow;
        for (int i = 0; i < 20; i++)
        {
            var price = 100 + i;
            klines.Add(new Kline
            {
                OpenTimeUtc = start.AddHours(i),
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 1,
                CloseTimeUtc = start.AddHours(i+1),
                IsClosed = true
            });
        }

        var strategy = new SequenceStrategy(new Dictionary<int, TradeSignal> { [14] = TradeSignal.Long });
        var settings = new AppSettings();
        var http = new HttpClient();
        var backtester = new Backtester(strategy, settings, http);
        var result = backtester.Run(klines);
        Assert.Equal(1, result.Trades);
        Assert.True(result.WinRate > 99m);
        Assert.Equal(settings.Rrr, result.AvgRr);
        Assert.True(result.Equity[^1] > 1000m);
    }
}
