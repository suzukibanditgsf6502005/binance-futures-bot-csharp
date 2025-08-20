using System.Collections.Generic;
using System.Threading.Tasks;
using Application;
using Infrastructure.Binance.Models;

namespace BinanceBot.Tests;

public class TimeStopTests
{
    private class FakeExchange : IExchangeClient
    {
        public int CloseCalls { get; private set; }

        public Task<List<Kline>> GetKlinesAsync(string symbol, string interval, int limit = 500) => Task.FromResult(new List<Kline>());
        public Task SetLeverageAsync(string symbol, int leverage) => Task.CompletedTask;
        public Task<OrderResult> PlaceMarketAsync(string symbol, OrderSide side, decimal quantity) => Task.FromResult(new OrderResult(true, null));
        public Task PlaceStopMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice) => Task.CompletedTask;
        public Task PlaceTakeProfitMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice) => Task.CompletedTask;
        public Task ClosePositionMarketAsync(string symbol, PositionSide posSide)
        {
            CloseCalls++;
            return Task.CompletedTask;
        }
        public Task<AccountInfo> GetAccountBalanceAsync() => Task.FromResult(new AccountInfo(0,0));
        public Task<PositionRisk> GetPositionRiskAsync(string symbol) => Task.FromResult(new PositionRisk());
        public Task<SymbolFilters> GetSymbolFiltersAsync(string symbol) => Task.FromResult(new SymbolFilters { Symbol = symbol, StepSize = 0.001m, TickSize = 0.1m, MinNotional = 5m });
    }

    [Fact]
    public void TimeStopTriggersMarketClose()
    {
        var filters = new SymbolFilters { Symbol = "TST", StepSize = 0.001m, TickSize = 0.1m, MinNotional = 5m };
        var trade = new global::TradeState(global::OrderSide.Buy, 100m, 99m, 103m);
        var exch = new FakeExchange();

        for (var i = 0; i < 3; i++)
        {
            var res = trade.Update(100.5m, 1m, 1m, 1m, filters, 3);
            if (res.TimeExit)
                exch.ClosePositionMarketAsync("TST", PositionSide.Long).Wait();
        }

        Assert.Equal(1, exch.CloseCalls);
    }
}

