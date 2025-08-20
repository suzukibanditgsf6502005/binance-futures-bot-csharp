namespace Infrastructure.Binance;

public sealed class BinanceOptions
{
    // Required by Microsoft.Extensions.Options
    public BinanceOptions() { }

    // Optional convenience ctor (keep if used elsewhere)
    public BinanceOptions(bool useTestnet) => UseTestnet = useTestnet;

    public bool UseTestnet { get; set; } = true;
    public int RecvWindowMs { get; set; } = 5000;

    public string BaseUrl =>
        UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";
}
