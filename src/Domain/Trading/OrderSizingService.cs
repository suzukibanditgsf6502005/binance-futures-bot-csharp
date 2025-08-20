using System;
using System.Globalization;
using Infrastructure.Binance.Models;
using Microsoft.Extensions.Logging;

namespace Domain.Trading;

public enum OrderType { Limit, Market }

public sealed class OrderSizingService
{
    private readonly ILogger<OrderSizingService> _logger;
    private readonly Func<string, SymbolFilters?> _getFilters;

    public OrderSizingService(ILogger<OrderSizingService> logger, Func<string, SymbolFilters?> getFilters)
    {
        _logger = logger;
        _getFilters = getFilters;
    }

    public bool TrySize(
        string symbol,
        OrderType orderType,
        decimal entryPrice,
        decimal stopPrice,
        decimal riskUsd,
        out decimal qtyRounded,
        out string reason)
    {
        qtyRounded = 0m;
        reason = string.Empty;

        var f = _getFilters(symbol);
        if (f is null)
        {
            reason = "No filters.";
            return false;
        }

        var stopDistance = Math.Abs(entryPrice - stopPrice);
        if (stopDistance <= 0m)
        {
            reason = "Invalid stop distance.";
            return false;
        }

        var qtyRaw = riskUsd / stopDistance;
        var step = f.StepSize > 0m ? f.StepSize : 0.000001m;

        qtyRounded = Math.Round(qtyRaw / step) * step;

        var minQty = orderType == OrderType.Market && f.MarketMinQty.HasValue
            ? f.MarketMinQty.Value
            : (f.MinQty ?? step);

        if (qtyRounded < step)
            qtyRounded = 0m;

        decimal RequiredRisk(decimal q) => q * stopDistance;

        if (qtyRounded < minQty)
        {
            var neededQty = Math.Ceiling(minQty / step) * step;
            var neededRisk = RequiredRisk(neededQty);
            if (neededRisk > riskUsd)
            {
                reason = $"Below minQty. minQty={minQty} step={step} qtyRaw={qtyRaw} risk={riskUsd} neededRisk={neededRisk} stop={stopDistance} price={entryPrice}";
                return false;
            }
            qtyRounded = neededQty;
        }

        if (f.MinNotional.HasValue)
        {
            var notional = qtyRounded * entryPrice;
            if (notional < f.MinNotional.Value)
            {
                var neededQty = Math.Ceiling((f.MinNotional.Value / entryPrice) / step) * step;
                var neededRisk = RequiredRisk(neededQty);
                if (neededRisk > riskUsd)
                {
                    reason = $"Below minNotional. minNotional={f.MinNotional} notional={notional} step={step} qty={qtyRounded} risk={riskUsd} neededRisk={neededRisk} stop={stopDistance} price={entryPrice}";
                    return false;
                }
                qtyRounded = neededQty;
            }
        }

        if (qtyRounded <= 0m)
        {
            reason = $"Qty <= 0 after filters. qtyRaw={qtyRaw} step={step} risk={riskUsd} stop={stopDistance} price={entryPrice}";
            return false;
        }

        _logger.LogDebug("Sized {Symbol} {OrderType} qty={Qty} (raw={Raw}) risk={Risk} stop={Stop} step={Step} minQty={MinQty} minNotional={MinNotional}",
            symbol, orderType, qtyRounded, qtyRaw, riskUsd, stopDistance, step, minQty, f.MinNotional);
        return true;
    }
}
