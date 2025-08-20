using Application;
using Infrastructure.Binance.Models;

namespace Infrastructure;

public class AtrRiskManager : IRiskManager
{
    public decimal CalculateQty(decimal balance, decimal atr, decimal price, AppSettings settings, SymbolFilters filters)
    {
        if (balance <= 0 || settings.RiskPerTradePct <= 0)
            return 0m;

        var rawQty = TradeMath.CalculatePositionQuantity(balance, settings.RiskPerTradePct, atr, settings.AtrMultiple, price, settings.Leverage);
        return filters.ClampQuantity(rawQty);
    }
}

