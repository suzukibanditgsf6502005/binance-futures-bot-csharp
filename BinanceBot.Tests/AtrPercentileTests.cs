using Application;

namespace BinanceBot.Tests;

public class AtrPercentileTests
{
    [Fact]
    public void PercentileRank_Computes()
    {
        var values = Enumerable.Range(1, 100).Select(i => (decimal)i).ToList();
        var pct = EntryFilters.PercentileRank(values, 50m);
        Assert.True(pct > 49 && pct < 51);
    }

    [Fact]
    public void GatingBounds()
    {
        var values = Enumerable.Range(1, 100).Select(i => (decimal)i).ToList();
        var inside = EntryFilters.PercentileRank(values, 20m);
        var outside = EntryFilters.PercentileRank(values, 95m);

        Assert.InRange(inside, 10m, 90m);
        Assert.False(outside >= 10m && outside <= 90m);
    }
}

