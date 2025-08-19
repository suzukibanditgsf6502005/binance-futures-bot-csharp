namespace Application;

public interface IOrderExecutor
{
    Task OpenWithBracketAsync(string symbol, OrderSide side, decimal qty, decimal price, decimal stopDistance, SymbolFilters filters);
    Task FlipCloseAsync(string symbol, PositionSide posSide);
}
