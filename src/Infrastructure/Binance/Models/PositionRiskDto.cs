using System.Text.Json.Serialization;

namespace Infrastructure.Binance.Models;

// Allow numbers coming as strings for this entire DTO
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class PositionRiskDto
{
    // Include only the fields the bot actually uses; add more if needed.
    public string Symbol { get; set; } = default!;
    public decimal PositionAmt { get; set; }           // "0.001" (string in JSON)
    public decimal EntryPrice { get; set; }            // "12345.67"
    public decimal MarkPrice { get; set; }             // "12346.01"
    public decimal UnRealizedProfit { get; set; }      // "0.00"
    public int Leverage { get; set; }                  // "3"
    public bool Isolated { get; set; }                 // true/false
    public decimal IsolatedMargin { get; set; }        // "0.00"
    public decimal MaxNotionalValue { get; set; }      // "1000000"
    public decimal LiquidationPrice { get; set; }      // "0.0"
    // add any other fields you consume elsewhere
}
