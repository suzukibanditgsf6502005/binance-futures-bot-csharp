namespace Infrastructure.Binance;

public interface IBinanceClock
{
    long UtcNowMsAdjusted();
}

