using BkkNotifyService;
using Microsoft.Extensions.Options;
using BkkNotifyService.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BkkNotifyBackgroundService>();

builder.Services.AddTransient<ITelegramBotClient, TelegramBotClient>(serviceProvider =>
{
    var token = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.Token;
    return new(token);
});

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.TELEGRAM));

var host = builder.Build();
host.Run();