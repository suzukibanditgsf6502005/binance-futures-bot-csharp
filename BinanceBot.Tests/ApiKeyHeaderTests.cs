using System.Net;
using System.Net.Http;
using System.Linq;
using Infrastructure.Binance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BinanceBot.Tests;

public class ApiKeyHeaderTests
{
    private const string ApiKey = "test-key";

    private static BinanceFuturesClient CreateClient(FakeHandler handler)
    {
        var http = new HttpClient(handler);
        var settings = new AppSettings { ApiKey = ApiKey, ApiSecret = "secret" };
        var options = Options.Create(new BinanceOptions(true));
        return new BinanceFuturesClient(http, settings, options, new FakeClock(), NullLogger<BinanceFuturesClient>.Instance);
    }

    private class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void EnqueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task SetLeverageAsync_UsesApiKeyHeader()
    {
        var handler = new FakeHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        var client = CreateClient(handler);
        await client.SetLeverageAsync("BTCUSDT", 10);

        var req = handler.Requests.Single();
        Assert.True(req.Headers.TryGetValues("X-MBX-APIKEY", out var values));
        Assert.Equal(ApiKey, Assert.Single(values));
    }

    [Fact]
    public async Task GetPositionRiskAsync_UsesApiKeyHeader()
    {
        var handler = new FakeHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"symbol\":\"BTCUSDT\",\"positionAmt\":0}]")
        });

        var client = CreateClient(handler);
        await client.GetPositionRiskAsync("BTCUSDT");

        var req = handler.Requests.Single();
        Assert.True(req.Headers.TryGetValues("X-MBX-APIKEY", out var values));
        Assert.Equal(ApiKey, Assert.Single(values));
    }
    private sealed class FakeClock : IBinanceClock
    {
        public long UtcNowMsAdjusted() => 0;
    }
}

