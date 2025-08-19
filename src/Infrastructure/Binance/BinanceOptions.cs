namespace Infrastructure.Binance;

public class BinanceOptions
{
    public bool UseTestnet { get; }
    public string BaseUrl { get; }

    public BinanceOptions(bool useTestnet)
    {
        UseTestnet = useTestnet;
        BaseUrl = useTestnet
            ? "https://testnet.binancefuture.com"
            : "https://fapi.binance.com";
    }
}
