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
1. Install the .NET 8 SDK.
2. Export Testnet API keys:
   ```bash
   export BINANCE_API_KEY=...
   export BINANCE_API_SECRET=...
   ```
3. Build the solution:
   ```bash
   dotnet build BinanceBot.sln
   ```
4. Run in dry mode:
   ```bash
   dotnet run --project BinanceBot.csproj -- --dry
   ```

## Logs
Logs are written to the console and to rolling files in `./logs` (e.g. `logs/log-YYYYMMDD.txt`).
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
