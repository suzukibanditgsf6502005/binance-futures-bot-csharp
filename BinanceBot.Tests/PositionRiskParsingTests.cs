using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Binance.Models;

public class PositionRiskParsingTests
{
    [Fact]
    public void Parses_numbers_provided_as_strings()
    {
        var json = @"[
          {
            ""symbol"": ""BTCUSDT"",
            ""positionAmt"": ""0.001"",
            ""entryPrice"": ""50000.00"",
            ""markPrice"": ""50010.00"",
            ""unRealizedProfit"": ""0.10"",
            ""leverage"": ""3"",
            ""isolated"": true,
            ""isolatedMargin"": ""12.34"",
            ""maxNotionalValue"": ""1000000"",
            ""liquidationPrice"": ""0""
          }
        ]";

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        var list = JsonSerializer.Deserialize<List<PositionRiskDto>>(json, options);
        Assert.NotNull(list);
        Assert.Single(list!);
        var r = list![0];
        Assert.Equal("BTCUSDT", r.Symbol);
        Assert.Equal(0.001m, r.PositionAmt);
        Assert.Equal(50000.00m, r.EntryPrice);
        Assert.Equal(3, r.Leverage);
        Assert.True(r.Isolated);
    }
}
