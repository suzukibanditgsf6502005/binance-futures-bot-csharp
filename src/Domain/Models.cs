using System.Text.Json.Serialization;

public record AppSettings(
    string ApiKey,
    string ApiSecret,
    bool UseTestnet,
    int Leverage,
    decimal RiskPerTradePct,
    decimal AtrMultiple,
    decimal Rrr,
    string Interval,
    string[] Symbols)
{
    [JsonIgnore]
    public string BaseUrl => UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";

    public static AppSettings Load()
    {
        return new AppSettings(
            ApiKey: Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "YOUR_TESTNET_API_KEY",
            ApiSecret: Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? "YOUR_TESTNET_SECRET",
            UseTestnet: true,
            Leverage: 3,
            RiskPerTradePct: 0.01m,
            AtrMultiple: 1.5m,
            Rrr: 2.0m,
            Interval: "1h",
            Symbols: new[] { "BTCUSDT", "ETHUSDT" }
        );
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
