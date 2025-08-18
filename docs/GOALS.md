# Project Goals & Roadmap

## Goals
1. Safe dry-run on Testnet for 2–4 weeks
2. Stable RRR≥1.8 and drawdown < 10% in backtest/paper
3. Live trading with small capital (≈ 100 USDT), risk 1%/trade

## Milestones
- M1: Initial bot with EMA/RSI/ATR, brackets, CI (this week)
- M2: Telegram alerts + trailing stop (next week)
- M3: Funding blackout + volatility filter (week 3)
- M4: Basic backtest on historical klines (week 4)

## Operational Cadence
- Weekly review: PnL, winrate, avg RR, max DD
- Monthly param tuning

## Decisions Log (append)
- 2025-08-19: Chosen day-trading 1h; RRR=2; SL=1.5*ATR; leverage x3; risk 1%
