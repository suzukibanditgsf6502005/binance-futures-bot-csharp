using Infrastructure.Binance.Models;

namespace Application;

public interface IRiskManager
{
    decimal CalculateQty(decimal balance, decimal atr, decimal price, AppSettings settings, SymbolFilters filters);
}

