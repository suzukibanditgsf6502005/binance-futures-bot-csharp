using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Application;
using Infrastructure.Binance;
using Infrastructure;
using Domain.Trading;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

if (args.Contains("--backtest"))
{
    var symbol = args[Array.IndexOf(args, "--backtest") + 1];
    var from = DateTime.Parse(args[Array.IndexOf(args, "--from") + 1]);
    var to = DateTime.Parse(args[Array.IndexOf(args, "--to") + 1]);
    var interval = "1h";
    var idx = Array.IndexOf(args, "--interval");
    if (idx >= 0 && idx + 1 < args.Length)
        interval = args[idx + 1];

    var settings = new AppSettings { UseTestnet = false };
    using var http = new HttpClient(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        BaseAddress = new Uri(settings.BaseUrl)
    };
    var backtester = new Backtester(new EmaRsiStrategy(), settings, http);
    await backtester.RunAsync(symbol, from, to, interval);
    return;
}

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<AppSettings>()
            .Bind(context.Configuration)
            .PostConfigure(s =>
            {
                s.ApiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty;
                s.ApiSecret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? string.Empty;
            })
            .Validate(s => !string.IsNullOrWhiteSpace(s.ApiKey), "BINANCE_API_KEY is required")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ApiSecret), "BINANCE_API_SECRET is required")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);

        services.Configure<BinanceOptions>(context.Configuration.GetSection("Binance"));

        services.PostConfigure<BinanceOptions>(o =>
        {
            var cfg = context.Configuration;
            var useTestnet = cfg.GetValue<bool?>("UseTestnet");
            if (useTestnet.HasValue) o.UseTestnet = useTestnet.Value;

            var rw = cfg.GetValue<int?>("RecvWindowMs");
            if (rw.HasValue) o.RecvWindowMs = rw.Value;
        });

        var alertsEnabled = Environment.GetEnvironmentVariable("TELEGRAM_ALERTS_ENABLED") == "1";
        if (alertsEnabled)
        {
            var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
            var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(chatId))
                services.AddSingleton<IAlertService>(new TelegramAlertService(token, chatId));
            else
                services.AddSingleton<IAlertService, NoopAlertService>();
        }
        else
        {
            services.AddSingleton<IAlertService, NoopAlertService>();
        }

        services.AddHttpClient("BinancePublic", (sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<BinanceOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl);
        });

        services.AddSingleton<IBinanceClock, TimeSyncClock>();
        services.AddHostedService(sp => (TimeSyncClock)sp.GetRequiredService<IBinanceClock>());

        services.AddHttpClient<IExchangeClient, BinanceFuturesClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.All
            });
        services.AddSingleton<IStrategy, EmaRsiStrategy>();
        services.AddSingleton<IRiskManager, AtrRiskManager>();
        services.AddSingleton<IOrderExecutor, BracketOrderExecutor>();
        services.AddSingleton<ISymbolFiltersRepository, SymbolFiltersRepository>();
        services.AddSingleton<OrderSizingService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OrderSizingService>>();
            var repo = sp.GetRequiredService<ISymbolFiltersRepository>();
            return new OrderSizingService(logger, symbol => repo.TryGet(symbol, out var f) ? f : null);
        });
        services.AddSingleton(new BotOptions(args.Contains("--dry")));
        services.AddHostedService<BotHostedService>();
    });

try
{
    await builder.Build().RunAsync();
}
finally
{
    Log.CloseAndFlush();
}
