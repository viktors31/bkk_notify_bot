using BkkNotifyService;
using BkkNotifyService.Features;
using Microsoft.Extensions.Options;
using BkkNotifyService.Options;
using Microsoft.AspNetCore.Session;
using Serilog;
using Telegram.Bot;
using TL;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BkkNotifyBackgroundService>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.TELEGRAM));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.APP));

builder.Services.AddTransient<ITelegramBotClient, TelegramBotClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new(options.Token);
});


builder.Services.AddSingleton<WTelegram.Client>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new(options.ApiId, options.ApiHash, options.SessionPathname);
});

builder.Services.AddTransient<IHandler<MessageBase>, MessageHandler>();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day) // файл с ротацией по дням
    .CreateLogger();
builder.Logging.AddSerilog(); //логгирование в файл через serilog

var host = builder.Build();
host.Run();