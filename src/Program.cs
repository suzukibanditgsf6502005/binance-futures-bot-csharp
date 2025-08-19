using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Application;
using Infrastructure.Binance;

var builder = Host.CreateDefaultBuilder(args)
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
        services.AddSingleton(new BotOptions(args.Contains("--dry")));
        services.AddHostedService<BotHostedService>();
    });

await builder.Build().RunAsync();
