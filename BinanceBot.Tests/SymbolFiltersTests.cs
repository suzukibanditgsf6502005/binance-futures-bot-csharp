using Infrastructure.Binance.Models;

namespace BinanceBot.Tests;

public class SymbolFiltersTests
{
    [Fact]
    public void ClampQuantity_EdgeCases()
    {
        var filters = new SymbolFilters { Symbol = "TST", StepSize = 0.001m, TickSize = 0.01m, MinNotional = 5m };

        Assert.Equal(0.123m, filters.ClampQuantity(0.1234m));
        Assert.Equal(0m, filters.ClampQuantity(0.0009m));
        Assert.Equal(0m, filters.ClampQuantity(-1m));
    }

    [Fact]
    public void ClampPrice_EdgeCases()
    {
        var filters = new SymbolFilters { Symbol = "TST", StepSize = 0.001m, TickSize = 0.01m, MinNotional = 5m };

        Assert.Equal(123.45m, filters.ClampPrice(123.4567m));
        Assert.Equal(123.40m, filters.ClampPrice(123.40m));
    }
}
