using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application;
using Serilog;

namespace Infrastructure.Binance;

public class BinanceFuturesClient : IExchangeClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _secret;
    private static readonly Random _jitter = new();
    private readonly IBinanceClock _clock;

    public BinanceFuturesClient(HttpClient http, AppSettings settings, IBinanceClock clock)
    {
        _http = http;
        _apiKey = settings.ApiKey;
        _secret = settings.ApiSecret;
        _clock = clock;
    }

    public async Task<List<Kline>> GetKlinesAsync(string symbol, string interval, int limit = 500)
    {
        var url = $"/fapi/v1/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={Math.Clamp(limit, 1, 1500)}";
        var res = await SendWithRetryAsync(() => _http.GetAsync(url));
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        var list = new List<Kline>(arr.GetArrayLength());

        foreach (var e in arr.EnumerateArray())
        {
            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(e[0].GetInt64()).UtcDateTime;
            var open  = decimal.Parse(e[1].GetString()!, CultureInfo.InvariantCulture);
            var high  = decimal.Parse(e[2].GetString()!, CultureInfo.InvariantCulture);
            var low   = decimal.Parse(e[3].GetString()!, CultureInfo.InvariantCulture);
            var close = decimal.Parse(e[4].GetString()!, CultureInfo.InvariantCulture);
            var vol   = decimal.Parse(e[5].GetString()!, CultureInfo.InvariantCulture);
            var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(e[6].GetInt64()).UtcDateTime;

            list.Add(new Kline
            {
                OpenTimeUtc = openTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = vol,
                CloseTimeUtc = closeTime,
                IsClosed = true
            });
        }

        return list;
    }

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
            ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
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
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture),
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
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture),
            ["closePosition"] = "true",
            ["timeInForce"] = "GTC"
        };
        await SendSignedAsync(HttpMethod.Post, "/fapi/v1/order", p);
    }

    public async Task ClosePositionMarketAsync(string symbol, PositionSide posSide)
    {
        var pr = await GetPositionRiskAsync(symbol);
        var qty = Math.Abs(pr.PositionAmt);
        if (qty <= 0m) return;
        var side = pr.PositionAmt > 0 ? OrderSide.Sell : OrderSide.Buy;
        var p = new Dictionary<string, string>
        {
            ["symbol"] = symbol.ToUpper(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = qty.ToString(CultureInfo.InvariantCulture),
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
        var res = await SendWithRetryAsync(() => _http.GetAsync($"/fapi/v1/exchangeInfo?symbol={symbol.ToUpper()}"));
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
        args["timestamp"] = _clock.UtcNowMsAdjusted().ToString();

        var query = string.Join("&", args.OrderBy(k => k.Key).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var sig = Sign(query);
        var requestUri = path + "?" + query + "&signature=" + sig;

        var req = new HttpRequestMessage(method, requestUri);
        req.Headers.Add("X-MBX-APIKEY", _apiKey);
        var resp = await SendWithRetryAsync(() => _http.SendAsync(req));
        var body = await resp.Content.ReadAsStringAsync();
        return (resp.IsSuccessStatusCode, body);
    }

    public async Task<ServerTime> GetServerTimeAsync()
    {
        var r = await SendWithRetryAsync(() => _http.GetAsync("/fapi/v1/time"));
        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var ms = doc.RootElement.GetProperty("serverTime").GetInt64();
        return new ServerTime(DateTimeOffset.FromUnixTimeMilliseconds(ms));
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

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        return code == 429 || code >= 500;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> send)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await send();
            if (!ShouldRetry(response) || attempt == 4)
            {
                return response;
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(_jitter.Next(0, 1000));
            Log.Warning("HTTP {Status} received, retrying in {Delay} (attempt {Attempt})", (int)response.StatusCode, delay, attempt + 1);
            response.Dispose();
            await Task.Delay(delay);
        }

        throw new InvalidOperationException("Retry logic failure");
    }
}
