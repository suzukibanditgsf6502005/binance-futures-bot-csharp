using System.Net;
using System.Net.Http;
using System.Linq;
using Infrastructure.Binance;

namespace BinanceBot.Tests;

public class ApiKeyHeaderTests
{
    private const string ApiKey = "test-key";

    private static BinanceFuturesClient CreateClient(FakeHandler handler)
    {
        var http = new HttpClient(handler);
        var settings = new AppSettings { ApiKey = ApiKey, ApiSecret = "secret" };
        var options = new BinanceOptions(true);
        var signer = new Signer("secret");
        return new BinanceFuturesClient(http, settings, options, signer);
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
            Content = new StringContent("{\"serverTime\":0}")
        });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        var client = CreateClient(handler);
        await client.SetLeverageAsync("BTCUSDT", 10);

        var req = handler.Requests.Last();
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
}

