using Application;
using Infrastructure;

namespace BinanceBot.Tests;

public class OrderExecutorDryRunTests
{
    private class FakeExchangeClient : IExchangeClient
    {
        public int PlaceMarketCalls { get; private set; }
        public int PlaceStopCalls { get; private set; }
        public int PlaceTpCalls { get; private set; }
        public int ClosePositionCalls { get; private set; }

        public Task<List<Kline>> GetKlinesAsync(string symbol, string interval, int limit = 500) => Task.FromResult(new List<Kline>());
        public Task SetLeverageAsync(string symbol, int leverage) => Task.CompletedTask;
        public Task<OrderResult> PlaceMarketAsync(string symbol, OrderSide side, decimal quantity)
        {
            PlaceMarketCalls++;
            return Task.FromResult(new OrderResult(true, null));
        }
        public Task PlaceStopMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
        {
            PlaceStopCalls++;
            return Task.CompletedTask;
        }
        public Task PlaceTakeProfitMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
        {
            PlaceTpCalls++;
            return Task.CompletedTask;
        }
        public Task ClosePositionMarketAsync(string symbol, PositionSide posSide)
        {
            ClosePositionCalls++;
            return Task.CompletedTask;
        }
        public Task<AccountInfo> GetAccountBalanceAsync() => Task.FromResult(new AccountInfo(0,0));
        public Task<PositionRisk> GetPositionRiskAsync(string symbol) => Task.FromResult(new PositionRisk());
        public Task<SymbolFilters> GetSymbolFiltersAsync(string symbol) => Task.FromResult(new SymbolFilters(0.001m,0.1m,5m));
    }

    [Fact]
    public async Task DryRunDoesNotCallSignedEndpoints()
    {
        var exchange = new FakeExchangeClient();
        var settings = AppSettings.Load();
        var executor = new BracketOrderExecutor(exchange, settings, new BotOptions(true), new NoopAlertService());
        var filters = new SymbolFilters(0.001m, 0.1m, 5m);

        await executor.OpenWithBracketAsync("BTCUSDT", OrderSide.Buy, 1m, 100m, 1m, filters);
        await executor.FlipCloseAsync("BTCUSDT", PositionSide.Long);

        Assert.Equal(0, exchange.PlaceMarketCalls);
        Assert.Equal(0, exchange.PlaceStopCalls);
        Assert.Equal(0, exchange.PlaceTpCalls);
        Assert.Equal(0, exchange.ClosePositionCalls);
    }
}
