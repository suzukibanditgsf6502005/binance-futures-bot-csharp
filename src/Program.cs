using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Application;
using Infrastructure.Binance;
using Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

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

        services.AddSingleton<HttpClient>(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            return new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.All
            })
            {
                BaseAddress = new Uri(settings.BaseUrl)
            };
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

        services.AddSingleton<IExchangeClient, BinanceFuturesClient>();
        services.AddSingleton<IStrategy, EmaRsiStrategy>();
        services.AddSingleton<IRiskManager, AtrRiskManager>();
        services.AddSingleton<IOrderExecutor, BracketOrderExecutor>();
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
