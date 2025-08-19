# Binance Futures Bot (C#/.NET 8)

Day-trading bot for Binance USDT-M Futures. Strategy: EMA(50/200) trend filter + RSI(14) pullback + ATR(14) based risk. Bracket
orders with SL/TP, fixed risk per trade, and symbol filters.

## Features
- Trend filter: EMA50 vs EMA200
- RSI(14) pullback validation
- ATR(14) position sizing: SL = ATR * 1.5 (configurable), TP = SL * 2 (RRR 1:2)
- Break-even at RR≥1 then ATR-based trailing stop (configurable)
- Per-symbol exchange filters (tickSize, stepSize, minNotional)
- Leverage setting per symbol on start
- Testnet-first (safe dry-run), flip to live later

## Quick start
1. Install the .NET 8 SDK.
2. Export **Testnet** API keys:
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

## Telegram alerts
Set up optional Telegram notifications for entry, SL/TP hits, flips, and errors.

1. Create a Telegram bot and get the token.
2. Export the variables:
   ```bash
   export TELEGRAM_TOKEN=...
   export TELEGRAM_CHAT_ID=...
   export TELEGRAM_ALERTS_ENABLED=1
   ```
   Remove `TELEGRAM_ALERTS_ENABLED` or set it to another value to disable alerts.

## Configuration

Settings are bound using the Options pattern. Non-secret values come from
`appsettings.{Environment}.json` files while API keys are read from environment
variables.

Hierarchy (lowest to highest):
1. `appsettings.json` (optional)
2. `appsettings.{Environment}.json`
3. Environment variables (`BINANCE_API_KEY`, `BINANCE_API_SECRET`)

Select environment via `DOTNET_ENVIRONMENT=Development` (default is
`Production`). Copy one of the provided examples and adjust:

```bash
cp appsettings.Development.json.example appsettings.Development.json
# or
cp appsettings.Production.json.example appsettings.Production.json
```

Example `appsettings.Development.json`:

```json
{
  "UseTestnet": true,
  "Leverage": 3,
  "RiskPerTradePct": 0.01,
  "AtrMultiple": 1.5,
  "Rrr": 2.0,
  "Interval": "1h",
  "Symbols": ["BTCUSDT","ETHUSDT"]
}
```

### AppSettings
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

## Deployment

### Docker

1. Copy one of the provided appsettings templates to `appsettings.Production.json` and adjust.
2. Export environment variables:
   - `BINANCE_API_KEY`
   - `BINANCE_API_SECRET`
   - *(optional)* `TELEGRAM_TOKEN`
   - *(optional)* `TELEGRAM_CHAT_ID`
   - *(optional)* `TELEGRAM_ALERTS_ENABLED=1`
3. Build and run:
   ```bash
   docker compose up -d
   ```
   Logs are persisted in `./logs` on the host via a volume.

The compose file also includes an optional [Seq](https://datalust.co/seq) container for structured log viewing.

### systemd (Ubuntu)

1. Publish the app to a folder, e.g. `/opt/binance-bot`.
2. Edit `deploy/binance-bot.service` and set paths and environment variables.
3. Install and start the service:
   ```bash
   sudo cp deploy/binance-bot.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable --now binance-bot.service
   ```
   Logs are written to `<working-directory>/logs`.

## License
Proprietary – personal use.
