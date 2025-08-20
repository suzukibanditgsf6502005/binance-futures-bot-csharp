using Infrastructure.Binance.Models;

namespace BinanceBot.Tests;

    public class TrailingStopTests
    {
        [Fact]
        public void BreakEvenAndTrailingWorkTogether()
        {
            var filters = new SymbolFilters { Symbol = "TST", StepSize = 0.001m, TickSize = 0.1m, MinNotional = 5m };
            var trade = new global::TradeState(global::OrderSide.Buy, 100m, 99m, 103m);

            var res = trade.Update(100.6m, 1m, 0.5m, 0.8m, filters, 0);
            Assert.True(res.StopChanged);
            Assert.Equal(100m, trade.Stop);

            res = trade.Update(101m, 1m, 0.5m, 0.8m, filters, 0);
            Assert.True(res.StopChanged);
            Assert.Equal(100.2m, trade.Stop);
        }

        [Fact]
        public void TrailingUpdatesAcrossCandles()
        {
            var filters = new SymbolFilters { Symbol = "TST", StepSize = 0.001m, TickSize = 0.1m, MinNotional = 5m };
            var trade = new global::TradeState(global::OrderSide.Buy, 100m, 98m, 104m);

            trade.Update(102m, 1m, 1m, 1m, filters, 0);
            Assert.Equal(100m, trade.Stop);

            trade.Update(105m, 1m, 1m, 1m, filters, 0);
            Assert.Equal(104m, trade.Stop);

            trade.Update(106m, 1.5m, 1m, 1m, filters, 0);
            Assert.Equal(104.5m, trade.Stop);

            trade.Update(107m, 3m, 1m, 1m, filters, 0);
            Assert.Equal(104.5m, trade.Stop);
        }
    }
