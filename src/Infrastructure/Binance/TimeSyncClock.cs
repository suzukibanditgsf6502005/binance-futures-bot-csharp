using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Binance;

public sealed class TimeSyncClock : IBinanceClock, IHostedService, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<TimeSyncClock> _logger;
    private long _offsetMs;
    private Timer? _timer;

    public TimeSyncClock(IHttpClientFactory httpFactory, ILogger<TimeSyncClock> logger, IOptions<BinanceOptions> opt)
    {
        _http = httpFactory.CreateClient("BinancePublic");
        _http.BaseAddress = new Uri(opt.Value.BaseUrl);
        _logger = logger;
    }

    public long UtcNowMsAdjusted() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Interlocked.Read(ref _offsetMs);

    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(async _ => await Sync(ct), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    private async Task Sync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync("/fapi/v1/time", ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<BinanceTimeDto>(cancellationToken: ct);
            if (json?.serverTime is long serverMs)
            {
                var localMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var off = serverMs - localMs;
                Interlocked.Exchange(ref _offsetMs, off);
                if (Math.Abs(off) > 1000)
                    _logger.LogWarning("Binance time offset = {Off} ms", off);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync Binance time");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private sealed record BinanceTimeDto(long serverTime);
}
