namespace Infrastructure.Binance;

public class BinanceOptions
{
    public int RecvWindowMs { get; set; } = 5000;
    public bool UseTestnet { get; set; }

    public string BaseUrl => UseTestnet
        ? "https://testnet.binancefuture.com"
        : "https://fapi.binance.com";
}
