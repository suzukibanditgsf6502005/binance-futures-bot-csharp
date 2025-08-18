# Architecture

**Runtime:** .NET 8 console app.

## Components
- **Exchange Client** (REST): minimal Binance Futures signer + endpoints used:
  - `/fapi/v1/klines`, `/fapi/v1/exchangeInfo`, `/fapi/v2/balance`, `/fapi/v2/positionRisk`, `/fapi/v1/leverage`, `/fapi/v1/order`
- **Indicators Layer:** `Skender.Stock.Indicators` for EMA/RSI/ATR
- **Strategy Engine:** builds signals from latest closed candle
- **Risk Manager:** computes qty from risk %, price, ATR; applies symbol filters
- **Order Manager:** market entry + bracket SL/TP (reduce-only close)
- **Scheduler:** 1-minute tick; acts on closed 1h candles

## Config & Secrets
- `AppSettings.Load()` reads env vars for API Keys; defaults to Testnet; later move to `appsettings.*.json` if desired (secrets excluded from git)

## Logging
- Console logs for actions, errors, and filter clamps

## Testing
- Unit tests for sizing math, price/qty clamping, and signal rules (TBD)

## Limitations / Notes
- No WebSockets (not needed for 1h). Can be added later for fills/positions stream.
- One-way mode assumed (no hedge mode)
