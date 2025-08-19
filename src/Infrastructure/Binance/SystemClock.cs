namespace Infrastructure.Binance;

public sealed class SystemClock : IBinanceClock
{
    public long UtcNowMsAdjusted() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

