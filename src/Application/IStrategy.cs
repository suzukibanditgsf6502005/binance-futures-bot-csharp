using Skender.Stock.Indicators;

namespace Application;

public enum TradeSignal
{
    None,
    Long,
    Short
}

public interface IStrategy
{
    TradeSignal Evaluate(IEnumerable<Quote> quotes);
}

