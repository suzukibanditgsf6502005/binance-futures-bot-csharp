using Application;
using Skender.Stock.Indicators;

namespace Infrastructure;

public class EmaRsiStrategy : IStrategy
{
    public TradeSignal Evaluate(IEnumerable<Quote> quotes)
    {
        var list = quotes.ToList();

        var ema50 = list.GetEma(50).ToList();
        var ema200 = list.GetEma(200).ToList();
        var rsi = list.GetRsi(14).ToList();

        if (ema50.Count < 5 || ema200.Count < 5 || rsi.Count < 5)
            return TradeSignal.None;

        var last = list[^1];
        var lastEma50 = (decimal)(ema50[^1].Ema ?? 0d);
        var lastEma200 = (decimal)(ema200[^1].Ema ?? 0d);
        var lastRsi = (decimal)(rsi[^1].Rsi ?? 50d);

        var trendUp = lastEma50 > lastEma200;
        var trendDown = lastEma50 < lastEma200;

        bool longSignal = trendUp && lastRsi > 45m && last.Close > lastEma50;
        bool shortSignal = trendDown && lastRsi < 55m && last.Close < lastEma50;

        if (longSignal) return TradeSignal.Long;
        if (shortSignal) return TradeSignal.Short;
        return TradeSignal.None;
    }
}

