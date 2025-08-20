using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Application;
using Infrastructure.Binance.Converters;
using Infrastructure.Binance.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Binance;

public class BinanceFuturesClient : IExchangeClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly BinanceOptions _options;
    private static readonly Random _jitter = new();
    private readonly Signer _signer;
    private readonly IBinanceClock _clock;
    private readonly ILogger<BinanceFuturesClient> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new DecimalStringConverter() }
    };

    public BinanceFuturesClient(
        HttpClient http,
        AppSettings settings,
        IOptions<BinanceOptions> options,
        IBinanceClock clock,
        ILogger<BinanceFuturesClient> logger)
    {
        _http = http;
        _apiKey = settings.ApiKey;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _signer = new Signer(settings.ApiSecret);
        _clock = clock;
        _logger = logger;
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
            var open = decimal.Parse(e[1].GetString()!, CultureInfo.InvariantCulture);
            var high = decimal.Parse(e[2].GetString()!, CultureInfo.InvariantCulture);
            var low = decimal.Parse(e[3].GetString()!, CultureInfo.InvariantCulture);
            var close = decimal.Parse(e[4].GetString()!, CultureInfo.InvariantCulture);
            var vol = decimal.Parse(e[5].GetString()!, CultureInfo.InvariantCulture);
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
        const string path = "/fapi/v1/leverage";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["leverage"] = leverage.ToString(CultureInfo.InvariantCulture)
        };

        using var req = CreateSignedRequest(HttpMethod.Post, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();

        _logger.LogInformation("Leverage changed on {Symbol} -> x{Lev}. Response: {Body}", symbol, leverage, body);
    }

    public async Task<OrderResult> PlaceMarketAsync(string symbol, OrderSide side, decimal quantity)
    {
        const string path = "/fapi/v1/order";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
            ["reduceOnly"] = "false"
        };

        using var req = CreateSignedRequest(HttpMethod.Post, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
            return new OrderResult(false, body);
        }
        return new OrderResult(true, null);
    }

    public async Task PlaceStopMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
    {
        const string path = "/fapi/v1/order";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "STOP_MARKET",
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture),
            ["closePosition"] = "true",
            ["timeInForce"] = "GTC"
        };

        using var req = CreateSignedRequest(HttpMethod.Post, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();
    }

    public async Task PlaceTakeProfitMarketCloseAsync(string symbol, OrderSide side, decimal stopPrice)
    {
        const string path = "/fapi/v1/order";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "TAKE_PROFIT_MARKET",
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture),
            ["closePosition"] = "true",
            ["timeInForce"] = "GTC"
        };

        using var req = CreateSignedRequest(HttpMethod.Post, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();
    }

    public async Task ClosePositionMarketAsync(string symbol, PositionSide _)
    {
        var pr = await GetPositionRiskAsync(symbol);
        var qty = Math.Abs(pr.PositionAmt);
        if (qty <= 0m) return;
        var side = pr.PositionAmt > 0 ? OrderSide.Sell : OrderSide.Buy;

        const string path = "/fapi/v1/order";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant(),
            ["side"] = side == OrderSide.Buy ? "BUY" : "SELL",
            ["type"] = "MARKET",
            ["quantity"] = qty.ToString(CultureInfo.InvariantCulture),
            ["reduceOnly"] = "true"
        };

        using var req = CreateSignedRequest(HttpMethod.Post, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();
    }

    public async Task<AccountInfo> GetAccountBalanceAsync()
    {
        const string path = "/fapi/v2/balance";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal);

        using var req = CreateSignedRequest(HttpMethod.Get, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();

        var arr = JsonSerializer.Deserialize<List<AccountBalance>>(body, _json)!;
        var usdt = arr.FirstOrDefault(x => x.Asset.Equals("USDT", StringComparison.OrdinalIgnoreCase));
        return new AccountInfo(usdt?.Balance ?? 0m, usdt?.AvailableBalance ?? 0m);
    }

    public async Task<PositionRisk> GetPositionRiskAsync(string symbol)
    {
        const string path = "/fapi/v2/positionRisk";
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["symbol"] = symbol.ToUpperInvariant()
        };

        using var req = CreateSignedRequest(HttpMethod.Get, path, dict);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Binance error {Status}: {Body}", (int)resp.StatusCode, body);
        }
        await resp.EnsureSuccessOrThrowAsync();

        var arr = JsonSerializer.Deserialize<List<PositionRiskDto>>(body, _json) ?? new();
        var dto = arr.FirstOrDefault();
        return new PositionRisk
        {
            Symbol = dto?.Symbol ?? symbol,
            PositionAmt = dto?.PositionAmt ?? 0m
        };
    }

    public async Task<SymbolFilters> GetSymbolFiltersAsync(string symbol)
    {
        var res = await SendWithRetryAsync(() => _http.GetAsync($"/fapi/v1/exchangeInfo?symbol={symbol.ToUpper()}"));
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var sym = doc.RootElement.GetProperty("symbols")[0];
        decimal tick = 0.01m, step = 0.001m;
        decimal? minQty = null, marketMinQty = null, minNotional = null;
        foreach (var f in sym.GetProperty("filters").EnumerateArray())
        {
            var type = f.GetProperty("filterType").GetString();
            if (type == "PRICE_FILTER")
            {
                tick = decimal.Parse(f.GetProperty("tickSize").GetString()!, CultureInfo.InvariantCulture);
            }
            else if (type == "LOT_SIZE")
            {
                step = decimal.Parse(f.GetProperty("stepSize").GetString()!, CultureInfo.InvariantCulture);
                if (f.TryGetProperty("minQty", out var mq))
                    minQty = decimal.Parse(mq.GetString()!, CultureInfo.InvariantCulture);
            }
            else if (type == "MARKET_LOT_SIZE")
            {
                if (f.TryGetProperty("minQty", out var mmq))
                    marketMinQty = decimal.Parse(mmq.GetString()!, CultureInfo.InvariantCulture);
            }
            else if (type == "MIN_NOTIONAL" || type == "NOTIONAL")
            {
                if (f.TryGetProperty("minNotional", out var mn))
                    minNotional = decimal.Parse(mn.GetString()!, CultureInfo.InvariantCulture);
                else if (f.TryGetProperty("notional", out var n))
                    minNotional = decimal.Parse(n.GetString()!, CultureInfo.InvariantCulture);
            }
        }

        return new SymbolFilters
        {
            Symbol = symbol.ToUpperInvariant(),
            TickSize = tick,
            StepSize = step,
            MinQty = minQty,
            MarketMinQty = marketMinQty,
            MinNotional = minNotional
        };
    }

    private HttpRequestMessage CreateSignedRequest(HttpMethod method, string path, SortedDictionary<string, string> parameters)
    {
        parameters["timestamp"] = _clock.UtcNowMsAdjusted().ToString(CultureInfo.InvariantCulture);
        parameters["recvWindow"] = _options.RecvWindowMs.ToString(CultureInfo.InvariantCulture);

        string Encode(string s) => Uri.EscapeDataString(s);
        var encodedQuery = string.Join("&", parameters.Select(kv => $"{kv.Key}={Encode(kv.Value)}"));
        var signature = _signer.SignToHex(encodedQuery);
        var url = path + "?" + encodedQuery + "&signature=" + signature;

        _logger.LogDebug("{Path} encodedQuery={Q} sig={S}", path, encodedQuery, signature);

        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("X-MBX-APIKEY", _apiKey);
        return req;
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
            _logger.LogWarning("HTTP {Status} received, retrying in {Delay} (attempt {Attempt})", (int)response.StatusCode, delay, attempt + 1);
            response.Dispose();
            await Task.Delay(delay);
        }

        throw new InvalidOperationException("Retry logic failure");
    }
}

