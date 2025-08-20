using System.Net;
using System.Net.Http;
using System.Linq;
using Infrastructure.Binance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public class BinanceSigningTests
{
    [Fact]
    public void SignToHex_ComputesExpectedSignature()
    {
        const string secret = "testsecret";
        var signer = new Signer(secret);
        const string query = "recvWindow=5000&symbol=BTCUSDT&timestamp=1690000000000";
        var sig = signer.SignToHex(query);
        Assert.Equal("7cec10cb57fdea05659b3b4094a22ac97200ec3aac16ca3af568bae40e49b604", sig);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var resp = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(resp);
        }
    }

    [Fact]
    public async Task GetAccountBalanceAsync_HasApiKeyHeaderAndNoBody()
    {
        const string apiKey = "test-key";
        var handler = new FakeHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        });

        var http = new HttpClient(handler);
        var settings = new AppSettings { ApiKey = apiKey, ApiSecret = "testsecret" };
        var options = Options.Create(new BinanceOptions(true));
        var client = new BinanceFuturesClient(http, settings, options, new FakeClock(), NullLogger<BinanceFuturesClient>.Instance);

        await client.GetAccountBalanceAsync();

        var req = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.True(req.Headers.TryGetValues("X-MBX-APIKEY", out var values));
        Assert.Equal(apiKey, Assert.Single(values));
        Assert.Null(req.Content);
    }
    private sealed class FakeClock : IBinanceClock
    {
        public long UtcNowMsAdjusted() => 1690000000000;
    }
}
