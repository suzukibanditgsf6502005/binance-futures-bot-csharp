# Strategy

**Style:** Day trading on 1h (optionally 15m).

## Indicators
- **EMA(50/200)**: trend filter
- **RSI(14)**: pullback validation
- **ATR(14)**: stop distance & sizing

## Entry
- **Long:** EMA50 > EMA200, Close > EMA50, RSI > 45
- **Short:** EMA50 < EMA200, Close < EMA50, RSI < 55

## Exit (Brackets)
- **Stop-Loss:** `SL = ATR * AtrMultiple` (default 1.5)
- **Take-Profit:** `TP = SL * Rrr` (default 2.0)
- Both placed as `STOP_MARKET` / `TAKE_PROFIT_MARKET` with `closePosition=true`.

## Risk & Sizing
- **Risk per trade:** `RiskPerTradePct` of available USDT (default 1%)
- Position quantity (contracts): `qty = (risk / stopDistance) * leverage / price`, clamped to exchange filters

## Symbols & Leverage
- Start: `BTCUSDT`, `ETHUSDT`
- Leverage default: `x3`

## Ops Rules
- Only one open position per symbol; flip = close current, then consider opposite
- No trading on unclosed candle
- Testnet first, min. 2 weeks

## Future Enhancements
- Trailing stop (ATR or %)
- Break-even when RR≥1
- Telegram/Discord alerts
- Funding-time blackout windows
- Volatility filter (ATR percentile) and volume confirmation
