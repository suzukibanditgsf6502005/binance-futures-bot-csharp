using System.Diagnostics.CodeAnalysis;
using Infrastructure.Binance.Models;

namespace Application;

public interface ISymbolFiltersRepository
{
    void Set(SymbolFilters filters);
    bool TryGet(string symbol, [NotNullWhen(true)] out SymbolFilters? filters);
}
