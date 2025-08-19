namespace Infrastructure.Binance;

public sealed class BinanceOptions
{
    public BinanceOptions(bool useTestnet)
    {
        UseTestnet = useTestnet;
    }

    public bool UseTestnet { get; }
    public string BaseUrl => UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";

    // New: recvWindow support (default 5000 ms)
    public int RecvWindowMs { get; set; } = 5000;
}