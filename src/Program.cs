// .NET 8 console app — single-file starter bot for Binance USDT-M Futures (day trading)
// Strategy: EMA(50/200) trend filter + RSI(14) pullback entries on 1h; risk-based sizing; SL=1.5×ATR(14), TP=RRR×SL
// Supports Binance Futures Testnet by default (flip UseTestnet=false for live). No WebSockets — hourly candles are polled.
// NuGet: Skender.Stock.Indicators (>=2.6.1)
// Build: dotnet add package Skender.Stock.Indicators
// Run:   dotnet run --project .

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skender.Stock.Indicators; // indicators

var settings = AppSettings.Load();
bool dryRun = args.Contains("--dry");
var http = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    AutomaticDecompression = System.Net.DecompressionMethods.All
})
{
    BaseAddress = new Uri(settings.BaseUrl)
};

var binance = new BinanceFuturesClient(http, settings.ApiKey, settings.ApiSecret, settings.UseTestnet);

Console.WriteLine($"Binance Futures bot starting (Testnet={settings.UseTestnet}) @ {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
if (dryRun)
    Console.WriteLine("Dry-run mode: orders will not be placed.");

// Preload exchange filters per symbol for rounding
var symbolMeta = new Dictionary<string, SymbolFilters>(StringComparer.OrdinalIgnoreCase);
foreach (var s in settings.Symbols)
{
    symbolMeta[s] = await binance.GetSymbolFiltersAsync(s);
    Console.WriteLine($"Loaded filters for {s}: tickSize={symbolMeta[s].TickSize}, stepSize={symbolMeta[s].StepSize}");
}

// Main loop (1-minute tick; acts on closed 1h candle)
var interval = settings.Interval;
var timer = PeriodicTimerFactory.Create(TimeSpan.FromMinutes(1));

// Optional: set leverage per symbol once at start
foreach (var s in settings.Symbols)
{
    await binance.SetLeverageAsync(s, settings.Leverage);
    Console.WriteLine($"Leverage set to x{settings.Leverage} on {s}");
}

while (await timer.WaitForNextTickAsync())
{
    try
    {
        foreach (var symbol in settings.Symbols)
        {
            await RunSymbolAsync(symbol);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}\n{ex}");
    }
}

async Task RunSymbolAsync(string symbol)
{
    // 1) get recent klines (we fetch 500 for warmup)
    var klines = await binance.GetKlinesAsync(symbol, interval, 500);
    if (klines.Count == 0) return;

    // ensure we wait for candle close: act only when last kline is closed
    if (!klines[^1].IsClosed)
    {
        Console.WriteLine($"{symbol}: last candle not closed yet — skipping.");
        return;
    }

    // map to Skender quotes
    IEnumerable<Quote> quotes = klines.Select(k => new Quote
    {
        Date = DateTime.SpecifyKind(k.OpenTimeUtc, DateTimeKind.Utc),
        Open = k.Open,
        High = k.High,
        Low = k.Low,
        Close = k.Close,
        Volume = k.Volume
    });

    // indicators
    var ema50 = quotes.GetEma(50).ToList();
    var ema200 = quotes.GetEma(200).ToList();
    var rsi = quotes.GetRsi(14).ToList();
    var atr = quotes.GetAtr(14).ToList();

    if (ema50.Count < 5 || ema200.Count < 5 || rsi.Count < 5) return; // safety

    // use the last fully closed bar
    var last = klines[^1];
    var lastEma50 = (decimal)(ema50[^1].Ema ?? 0d);
    var lastEma200 = (decimal)(ema200[^1].Ema ?? 0d);
    var lastRsi = (decimal)(rsi[^1].Rsi ?? 50d);
    var lastAtr = (decimal)(atr[^1].Atr ?? 0d);

    // 2) trend + entry logic
    var trendUp = lastEma50 > lastEma200;
    var trendDown = lastEma50 < lastEma200;

    // simple pullback rules
    bool longSignal = trendUp && lastRsi > 45m && last.Close > lastEma50;
    bool shortSignal = trendDown && lastRsi < 55m && last.Close < lastEma50;

    // 3) position state
    var pos = await binance.GetPositionRiskAsync(symbol);
    var hasLong = pos.PositionAmt > 0m;
    var hasShort = pos.PositionAmt < 0m;

    // 4) risk-based sizing
    var account = await binance.GetAccountBalanceAsync();
    var usdtBalance = account.AvailableBalance;
    var riskPerTrade = settings.RiskPerTradePct * usdtBalance;
    if (riskPerTrade <= 0) return;
    var stopDistance = Math.Max(lastAtr * settings.AtrMultiple, 0.001m);

    // quantity in contract terms (USDT notionals): qty = (risk / stop) * leverage / price
    var rawQty = TradeMath.CalculatePositionQuantity(usdtBalance, settings.RiskPerTradePct, lastAtr, settings.AtrMultiple, last.Close, settings.Leverage);

    var filters = symbolMeta[symbol];
    var qty = filters.ClampQuantity(rawQty);

    if (qty <= 0m)
    {
        Console.WriteLine($"{symbol}: qty too small after filters — risk={riskPerTrade:F2} stop={stopDistance:F2} price={last.Close:F2}");
        return;
    }

    // 5) open/close logic (one-way mode). We open only if flat
    if (!hasLong && !hasShort)
    {
        if (longSignal)
        {
            await OpenWithBracketAsync(symbol, OrderSide.Buy, qty, last.Close, stopDistance);
        }
        else if (shortSignal)
        {
            await OpenWithBracketAsync(symbol, OrderSide.Sell, qty, last.Close, stopDistance);
        }
        else
        {
            Console.WriteLine($"{symbol}: no signal.");
        }
    }
    else
    {
        // Optional: if signal flips hard, flatten at market
        if (hasLong && shortSignal)
        {
            if (dryRun)
            {
                Console.WriteLine($"{symbol}: flip detected — [DRY] would close LONG at market");
            }
            else
            {
                Console.WriteLine($"{symbol}: flip detected — closing LONG at market");
                await binance.ClosePositionMarketAsync(symbol, PositionSide.Long);
            }
        }
        else if (hasShort && longSignal)
        {
            if (dryRun)
            {
                Console.WriteLine($"{symbol}: flip detected — [DRY] would close SHORT at market");
            }
            else
            {
                Console.WriteLine($"{symbol}: flip detected — closing SHORT at market");
                await binance.ClosePositionMarketAsync(symbol, PositionSide.Short);
            }
        }
    }
}

async Task OpenWithBracketAsync(string symbol, OrderSide side, decimal qty, decimal price, decimal stopDistance)
{
    // place SL and TP as reduce-only market triggers (closePosition=true)
    var slPrice = side == OrderSide.Buy ? price - stopDistance : price + stopDistance;
    var tpPrice = side == OrderSide.Buy ? price + stopDistance * settings.Rrr : price - stopDistance * settings.Rrr;

    slPrice = symbolMeta[symbol].ClampPrice(slPrice);
    tpPrice = symbolMeta[symbol].ClampPrice(tpPrice);

    if (dryRun)
    {
        Console.WriteLine($"{symbol}: [DRY] OPEN {side} qty={qty} @~{price:F2} | SL={slPrice:F2} TP={tpPrice:F2}");
        return;
    }

    var entry = await binance.PlaceMarketAsync(symbol, side, qty);
    if (!entry.Success)
    {
        Console.WriteLine($"{symbol}: entry failed — {entry.Error}");
        return;
    }

    await binance.PlaceStopMarketCloseAsync(symbol, side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy, slPrice);
    await binance.PlaceTakeProfitMarketCloseAsync(symbol, side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy, tpPrice);

    Console.WriteLine($"{symbol}: OPEN {side} qty={qty} @~{price:F2} | SL={slPrice:F2} TP={tpPrice:F2}");
}

// ===================== Models & helpers =====================

record AppSettings(
    string ApiKey,
    string ApiSecret,
    bool UseTestnet,
    int Leverage,
    decimal RiskPerTradePct,
    decimal AtrMultiple,
    decimal Rrr,
    string Interval,
    string[] Symbols)
{
    [JsonIgnore] public string BaseUrl => UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";

    public static AppSettings Load()
    {
        // hardcoded defaults; you can replace with reading appsettings.json
        return new AppSettings(
            ApiKey: Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? "YOUR_TESTNET_API_KEY",
            ApiSecret: Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? "YOUR_TESTNET_SECRET",
            UseTestnet: true, // flip to false for live
            Leverage: 3,
            RiskPerTradePct: 0.01m, // 1% risk per trade
            AtrMultiple: 1.5m, // SL distance
            Rrr: 2.0m, // take profit multiple
            Interval: "1h",
            Symbols: new[] { "BTCUSDT", "ETHUSDT" }
        );
    }
}

class BinanceFuturesClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _secret;
    private readonly bool _testnet;

    public BinanceFuturesClient(HttpClient http, string apiKey, string secret, bool testnet)
    {
        _http = http;
        _apiKey = apiKey;
        _secret = secret;
        _testnet = testnet;
    }

    // Public market data (no auth)
    public async Task<List<Kline>> GetKlinesAsync(string symbol, string interval, int limit = 500)
    {
        var url = $"/fapi/v1/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={Math.Clamp(limit, 1, 1500)}";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var raw = JsonSerializer.Deserialize<List<object[]>>(json)!;
        return raw.Select(r => new Kline
        {
            OpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)Convert.ToDouble(r[0]!)).UtcDateTime,
            Open = decimal.Parse(r[1]!.ToString()!),
            High = decimal.Parse(r[2]!.ToString()!),
            Low = decimal.Parse(r[3]!.ToString()!),
            Close = decimal.Parse(r[4]!.ToString()!),
            Volume = decimal.Parse(r[5]!.ToString()!),
            CloseTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)Convert.ToDouble(r[6]!)).UtcDateTime,
            IsClosed = true // klines endpoint returns closed candles up to latest closed
        }).ToList();
    }

    public async Task<ServerTime> GetServerTimeAsync()
    {
        var r = await _http.GetAsync("/fapi/v1/time");
        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var ms = doc.RootElement.GetProperty("serverTime").GetInt64();
        return new ServerTime(DateTimeOffset.FromUnixTimeMilliseconds(ms));
    }

    // Account & trading (signed)
    public async Task SetLeverageAsync(string symbol, int leverage)
    {
        var payload = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["leverage"] = leverage.ToString()
        };
        await SendSignedAsync(HttpMethod.Post, "/fapi/v1/leverage", payload);
    }

    public async Task<OrderResult> PlaceMarketAsync(string symbol, OrderSide side, decimal quantity)
    {
        var p = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["reduceOnly"] = "false"
        };
        var (ok, body) = await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", p);
        return new OrderResult(ok, ok ? null : body);
    }

    public async Task PlaceStopMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
    {
        var p = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "STOP_MARKET",
            ["stopPrice"] = stopPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["closePosition"] = "true",
            ["timeInForce"] = "GTC"
        };
        await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", p);
    }

    public async Task PlaceTakeProfitMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
    {
        var p = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "TAKE_PROFIT_MARKET",
            ["stopPrice"] = stopPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["closePosition"] = "true",
            ["timeInForce"] = "GTC"
        };
        await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", p);
    }

    public async Task ClosePositionMarketAsync(string symbol, PositionSide posSide)
    {
        // Close by placing reduceOnly market in opposite direction using current abs position qty
        var pr = await GetPositionRiskAsync(symbol);
        var qty = Math.Abs(pr.PositionAmt);
        if (qty <= 0m) return;
        var side = pr.PositionAmt > 0 ? OrderSide.Sell : OrderSide.Buy;
        var p = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["reduceOnly"] = "true"
        };
        await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", p);
    }

    public async Task<AccountInfo> GetAccountBalanceAsync()
    {
        var (ok, body) = await SendSignedAsync(HttpMethod.Get, "/fapi/v2/balance");
        if (!ok) throw new Exception(body);
        var arr = JsonSerializer.Deserialize<List<AccountBalance>>(body)!;
        var usdt = arr.FirstOrDefault(x => x.Asset.Equals("USDT", StringComparison.OrdinalIgnoreCase));
        return new AccountInfo(usdt?.Balance ?? 0m, usdt?.AvailableBalance ?? 0m);
    }

    public async Task<PositionRisk> GetPositionRiskAsync(string symbol)
    {
        var (ok, body) = await SendSignedAsync(HttpMethod.Get, "/fapi/v2/positionRisk", new Dictionary<string, string> { ["symbol"] = symbol.ToUpper() });
        if (!ok) throw new Exception(body);
        var arr = JsonSerializer.Deserialize<List<PositionRisk>>(body)!;
        return arr[0];
    }

    public async Task<SymbolFilters> GetSymbolFiltersAsync(string symbol)
    {
        var res = await _http.GetAsync($"/fapi/v1/exchangeInfo?symbol={symbol.ToUpper()}");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var sym = doc.RootElement.GetProperty("symbols")[0];
        decimal tick = 0.01m, step = 0.001m, minNotional = 5m;
        foreach (var f in sym.GetProperty("filters").EnumerateArray())
        {
            var type = f.GetProperty("filterType").GetString();
            if (type == "PRICE_FILTER") tick = decimal.Parse(f.GetProperty("tickSize").GetString()!);
            if (type == "LOT_SIZE") step = decimal.Parse(f.GetProperty("stepSize").GetString()!);
            if (type == "MIN_NOTIONAL" && f.TryGetProperty("notional", out var n)) minNotional = decimal.Parse(n.GetString()!);
        }

        return new SymbolFilters(step, tick, minNotional);
    }

    private async Task<(bool ok, string body)> SendSignedAsync(HttpMethod method, string path, Dictionary<string, string>? args = null)
    {
        args ??= new();
        // timestamp with small server-time sync
        var server = await GetServerTimeAsync();
        args["timestamp"] = server.Time.ToUnixTimeMilliseconds().ToString();

        var query = string.Join("&", args.OrderBy(k => k.Key).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var sig = Sign(query);
        var requestUri = path + "?" + query + "&signature=" + sig;

        var req = new HttpRequestMessage(method, requestUri);
        req.Headers.Add("X-MBX-APIKEY", _apiKey);
        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.IsSuccessStatusCode, body);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var c = new char[bytes.Length * 2];
        int b;
        for (int i = 0; i < bytes.Length; i++)
        {
            b = bytes[i] >> 4;
            c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
            b = bytes[i] & 0xF;
            c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
        }

        return new string(c);
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return ToHex(hash).ToLowerInvariant();
    }
}

// ============ domain models ============

// Fix: property name cannot be the same as the type name.
// Was: record ServerTime(DateTimeOffset ServerTime);
// Now: record ServerTime(DateTimeOffset Time);
record ServerTime(DateTimeOffset Time);

record Kline
{
    public DateTime OpenTimeUtc { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
    public DateTime CloseTimeUtc { get; init; }
    public bool IsClosed { get; init; }
}

record AccountInfo(decimal Balance, decimal AvailableBalance);

class AccountBalance
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("balance")] public decimal Balance { get; set; }
    [JsonPropertyName("availableBalance")] public decimal AvailableBalance { get; set; }
}

class PositionRisk
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("positionAmt")] public decimal PositionAmt { get; set; } // +long, -short
}

record OrderResult(bool Success, string? Error);

enum OrderSide
{
    Buy,
    Sell
}

enum PositionSide
{
    Long,
    Short
}

public static class TradeMath
{
    public static decimal CalculatePositionQuantity(decimal balance, decimal riskPct, decimal atr, decimal atrMultiple, decimal price, int leverage)
    {
        var risk = balance * riskPct;
        var stopDistance = Math.Max(atr * atrMultiple, 0.001m);
        return (risk / stopDistance) * leverage / price;
    }
}

public record SymbolFilters(decimal StepSize, decimal TickSize, decimal MinNotional)
{
    public decimal ClampQuantity(decimal qty)
    {
        if (qty <= 0) return 0;
        return Math.Floor(qty / StepSize) * StepSize;
    }

    public decimal ClampPrice(decimal price)
    {
        return Math.Round(Math.Floor(price / TickSize) * TickSize, 8);
    }
}

static class PeriodicTimerFactory
{
    public static PeriodicTimer Create(TimeSpan period) => new(period);
}