using BkkNotifyService;
using BkkNotifyService.Features;
using Microsoft.Extensions.Options;
using BkkNotifyService.Options;
using Telegram.Bot;
using TL;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BkkNotifyBackgroundService>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.TELEGRAM));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.APP));

builder.Services.AddTransient<ITelegramBotClient, TelegramBotClient>(serviceProvider =>
{
    var token = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.Token;
    return new(token);
});

builder.Services.AddSingleton<WTelegram.Client>(serviceProvider =>
{
    var apiId = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.ApiId;
    var apiHash = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.ApiHash;
    return new(apiId, apiHash);
});

builder.Services.AddTransient<IHandler<MessageBase>, MessageHandler>();

//builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.TELEGRAM));
//builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.APP));

var host = builder.Build();
host.Run();