using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Application;
using Infrastructure.Binance.Models;

namespace Infrastructure;

public sealed class SymbolFiltersRepository : ISymbolFiltersRepository
{
    private readonly ConcurrentDictionary<string, SymbolFilters> _filters = new(StringComparer.OrdinalIgnoreCase);

    public void Set(SymbolFilters filters)
    {
        _filters[filters.Symbol] = filters;
    }

    public bool TryGet(string symbol, [NotNullWhen(true)] out SymbolFilters? filters)
    {
        return _filters.TryGetValue(symbol, out filters);
    }
}
