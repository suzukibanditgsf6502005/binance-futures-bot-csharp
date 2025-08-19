using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var settings = AppSettings.Load();
        services.AddSingleton(settings);

        services.AddSingleton<HttpClient>(_ =>
            new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.All
            })
            {
                BaseAddress = new Uri(settings.BaseUrl)
            });

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
