namespace BinanceBot.Tests;

public class TrailingStopTests
{
    [Fact]
    public void BreakEvenTriggersAtConfiguredRr()
    {
        var filters = new global::SymbolFilters(0.001m, 0.1m, 5m);
        var trade = new global::TradeState(global::OrderSide.Buy, 100m, 98m, 104m);

        var changed = trade.Update(101m, 1m, 1m, 1m, filters);
        Assert.False(changed);
        Assert.Equal(98m, trade.Stop);

        changed = trade.Update(102m, 1m, 1m, 1m, filters);
        Assert.True(changed);
        Assert.Equal(100m, trade.Stop);
    }

    [Fact]
    public void TrailingUpdatesAcrossCandles()
    {
        var filters = new global::SymbolFilters(0.001m, 0.1m, 5m);
        var trade = new global::TradeState(global::OrderSide.Buy, 100m, 98m, 104m);

        trade.Update(102m, 1m, 1m, 1m, filters);
        Assert.Equal(100m, trade.Stop);

        trade.Update(105m, 1m, 1m, 1m, filters);
        Assert.Equal(104m, trade.Stop);

        trade.Update(106m, 1.5m, 1m, 1m, filters);
        Assert.Equal(104.5m, trade.Stop);

        trade.Update(107m, 3m, 1m, 1m, filters);
        Assert.Equal(104.5m, trade.Stop);
    }
}
