using System.Collections.Generic;
using Infrastructure.Binance.Models;

namespace Application;

public interface IExchangeClient
{
    Task<List<Kline>> GetKlinesAsync(string symbol, string interval, int limit = 500);
    Task SetLeverageAsync(string symbol, int leverage);
    Task<OrderResult> PlaceMarketAsync(string symbol, OrderSide side, decimal quantity);
    Task PlaceStopMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice);
    Task PlaceTakeProfitMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice);
    Task ClosePositionMarketAsync(string symbol, PositionSide posSide);
    Task<AccountInfo> GetAccountBalanceAsync();
    Task<PositionRisk> GetPositionRiskAsync(string symbol);
    Task<SymbolFilters> GetSymbolFiltersAsync(string symbol);
}
