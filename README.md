# Binance Futures Bot (C#/.NET 8)

Day-trading bot for Binance USDT-M Futures. Strategy: EMA(50/200) trend filter + RSI(14) pullback + ATR(14) based risk. Bracket orders with SL/TP, fixed risk per trade, and symbol filters.

## Features
- Trend filter: EMA50 vs EMA200
- RSI(14) pullback validation
- ATR(14) position sizing: SL = ATR * 1.5 (configurable), TP = SL * 2 (RRR 1:2)
- Per-symbol exchange filters (tickSize, stepSize, minNotional)
- Leverage setting per symbol on start
- Testnet-first (safe dry-run), flip to live later

## Quick start
1. **.NET 8 SDK** installed.
2. Set environment variables (Testnet keys):
   - `BINANCE_API_KEY`, `BINANCE_API_SECRET`
3. Install indicators:
   ```bash
   dotnet add package Skender.Stock.Indicators
   ```
4. Run:
   ```bash
   dotnet run
   ```

## Config (AppSettings)
- `UseTestnet`: `true` for dry-run
- `Leverage`: e.g. `3`
- `RiskPerTradePct`: e.g. `0.01` (1% risk)
- `AtrMultiple`: e.g. `1.5`
- `Rrr`: e.g. `2.0`
- `Interval`: `1h` (day trading)
- `Symbols`: `["BTCUSDT","ETHUSDT"]`

## Safety
- API keys: trade-only; never enable withdrawals; consider IP allowlist
- Start on Testnet; move to live after stable results
- Futures are risky; use strict risk management

## Folders
- `src/` main app
- `docs/` technical docs (strategy, architecture, goals)
- `.github/workflows/` CI

## License
Proprietary – personal use.
