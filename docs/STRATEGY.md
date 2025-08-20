# Strategy

**Style:** Day trading on 1h (optionally 15m).

## Indicators
- **EMA(50/200)**: trend filter
- **RSI(14)**: pullback validation
- **ATR(14)**: stop distance & sizing

## Entry
- **Long:** EMA50 > EMA200, Close > EMA50, RSI > 45
- **Short:** EMA50 < EMA200, Close < EMA50, RSI < 55

## Exit
- **Stop-Loss:** `SL = ATR * AtrMultiple` (default 1.5)
- **Take-Profit:** `TP = SL * Rrr` (default 2.0)
- **Break-even:** move SL to entry when RR ≥ `BreakEvenAtR` (default 1.0)
- **Trailing:** after break-even, trail SL by `ATR * TrailingAtrMultiple` (default 1.0)
- Orders placed as `STOP_MARKET` / `TAKE_PROFIT_MARKET` with `closePosition=true`.

## Risk & Sizing
- **Risk per trade:** `RiskPerTradePct` of available USDT (default 1%)
- Position quantity (contracts): `qty = (risk / stopDistance) * leverage / price`, clamped to exchange filters

## Symbols & Leverage
- Start: `BTCUSDT`, `ETHUSDT`
- Leverage default: `x3`

## Ops Rules
- Only one open position per symbol; flip = close current, then consider opposite
- No trading on unclosed candle
- Skip new entries within configurable minutes around funding times (00:00/08:00/16:00 UTC)
- Gate entries by ATR percentile range
- Testnet first, min. 2 weeks

## Future Enhancements
- Telegram/Discord alerts
- Funding-time blackout windows
- Volatility filter (ATR percentile) and volume confirmation
