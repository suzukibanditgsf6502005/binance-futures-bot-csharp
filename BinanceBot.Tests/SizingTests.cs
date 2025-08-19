namespace BinanceBot.Tests;

public class SizingTests
{
    [Fact]
    public void CalculatesQuantityCorrectly()
    {
        decimal balance = 1000m;
        decimal riskPct = 0.01m;
        decimal atr = 1m;
        decimal atrMultiple = 2m;
        decimal price = 100m;
        int leverage = 5;

        var qty = global::TradeMath.CalculatePositionQuantity(balance, riskPct, atr, atrMultiple, price, leverage);
        Assert.Equal(0.25m, qty);
    }
}
