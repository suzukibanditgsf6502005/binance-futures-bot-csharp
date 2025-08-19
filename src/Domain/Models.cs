using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

public record AppSettings
{
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "YOUR_TESTNET_API_KEY";
    public string ApiSecret { get; init; } = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? "YOUR_TESTNET_SECRET";
    public bool UseTestnet { get; init; } = true;
    public int Leverage { get; init; } = 3;
    public decimal RiskPerTradePct { get; init; } = 0.01m;
    public decimal AtrMultiple { get; init; } = 1.5m;
    public decimal Rrr { get; init; } = 2.0m;
    public decimal BreakEvenAtRr { get; init; } = 1.0m;
    public decimal AtrTrailMultiple { get; init; } = 1.0m;
    public string Interval { get; init; } = "1h";
    public string[] Symbols { get; init; } = new[] { "BTCUSDT", "ETHUSDT" };
    public int FundingBlackoutMinutes { get; init; } = 10;
    public decimal AtrPercentileMin { get; init; } = 0m;
    public decimal AtrPercentileMax { get; init; } = 100m;

    [JsonIgnore]
    public string BaseUrl => UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";

    public static AppSettings Load(IConfiguration? config = null)
    {
        var settings = config?.Get<AppSettings>() ?? new AppSettings();

        var apiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET");

        if (!string.IsNullOrWhiteSpace(apiKey))
            settings = settings with { ApiKey = apiKey };
        if (!string.IsNullOrWhiteSpace(apiSecret))
            settings = settings with { ApiSecret = apiSecret };

        return settings;
    }
}

public record ServerTime(DateTimeOffset Time);

public record Kline
{
    public DateTime OpenTimeUtc { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public DateTime CloseTimeUtc { get; init; }
    public bool IsClosed { get; init; }
}

public record AccountInfo(decimal Balance, decimal AvailableBalance);

public class AccountBalance
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("balance")] public decimal Balance { get; set; }
    [JsonPropertyName("availableBalance")] public decimal AvailableBalance { get; set; }
}

public class PositionRisk
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("positionAmt")] public decimal PositionAmt { get; set; }
}

public record OrderResult(bool Success, string? Error);

public enum OrderSide
{
    Buy,
    Sell
}

public enum PositionSide
{
    Long,
    Short
}

public static class TradeMath
{
    public static decimal CalculatePositionQuantity(decimal balance, decimal riskPct, decimal atr, decimal atrMultiple, decimal price, int leverage)
    {
        var risk = balance * riskPct;
        var stopDistance = Math.Max(atr * atrMultiple, 0.001m);
        return (risk / stopDistance) * leverage / price;
    }
}

public class TradeState
{
    public OrderSide Side { get; }
    public decimal Entry { get; }
    public decimal Stop { get; private set; }
    public decimal Tp { get; }
    public decimal InitialRisk { get; }
    public bool BreakEvenActive { get; private set; }

    public TradeState(OrderSide side, decimal entry, decimal stop, decimal tp)
    {
        Side = side;
        Entry = entry;
        Stop = stop;
        Tp = tp;
        InitialRisk = Math.Abs(entry - stop);
    }

    public bool Update(decimal close, decimal atr, decimal breakEvenAtRr, decimal atrTrailMultiple, SymbolFilters filters)
    {
        var oldStop = Stop;
        var beTriggered = false;

        if (!BreakEvenActive)
        {
            var rr = Side == OrderSide.Buy ? (close - Entry) / InitialRisk : (Entry - close) / InitialRisk;
            if (rr >= breakEvenAtRr)
            {
                Stop = filters.ClampPrice(Entry);
                BreakEvenActive = true;
                beTriggered = true;
            }
        }

        if (BreakEvenActive && !beTriggered)
        {
            var candidate = Side == OrderSide.Buy
                ? close - atr * atrTrailMultiple
                : close + atr * atrTrailMultiple;
            candidate = filters.ClampPrice(candidate);

            if (Side == OrderSide.Buy)
                Stop = Math.Max(Stop, candidate);
            else
                Stop = Math.Min(Stop, candidate);
        }

        return Stop != oldStop;
    }
}

public record SymbolFilters(decimal StepSize, decimal TickSize, decimal MinNotional)
{
    public decimal ClampQuantity(decimal qty)
    {
        if (qty <= 0) return 0;
        return Math.Floor(qty / StepSize) * StepSize;
    }

    public decimal ClampPrice(decimal price)
    {
        return Math.Round(Math.Floor(price / TickSize) * TickSize, 8);
    }
}
