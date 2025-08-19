using Application;

namespace BinanceBot.Tests;

public class FundingBlackoutTests
{
    [Theory]
    [InlineData("2024-01-01T00:05:00Z", true)]
    [InlineData("2024-01-01T07:55:00Z", true)]
    [InlineData("2024-01-01T15:52:00Z", true)]
    [InlineData("2024-01-01T16:11:00Z", false)]
    [InlineData("2024-01-01T12:00:00Z", false)]
    [InlineData("2024-01-01T23:55:00Z", true)]
    public void WindowDetection(string isoTime, bool expected)
    {
        var time = DateTimeOffset.Parse(isoTime);
        Assert.Equal(expected, EntryFilters.IsFundingBlackout(time, 10));
    }
}

