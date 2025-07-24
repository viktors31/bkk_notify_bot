using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BkkNotifyService;

public class BkkNotifyBackgroundService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<BkkNotifyBackgroundService> _logger;

    public BkkNotifyBackgroundService(
        ITelegramBotClient botClient,
        ILogger<BkkNotifyBackgroundService> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BKK Notify Service v1.0.0 started at: {time}", DateTimeOffset.Now);
        await AuthorizeClient(stoppingToken); // Пока не пройдет авторизация дальше не пойдет
        while (!stoppingToken.IsCancellationRequested)
        {
            
        }
    }

    private async Task ListenChatIds(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Сервис запущен в режиме прослушки chatId.");
        int? offset = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await _botClient.GetUpdates(offset, timeout: 2);
            foreach (var update in updates)
            {
                offset = update.Id + 1;
                try
                {
                    switch (update)
                    {
                        case { Message: { } msg }: _logger.LogInformation($"Received {msg.Chat}: {msg.Text}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    
                }

                if (cancellationToken.IsCancellationRequested) break;
            }
        }
    }

    private async Task AuthorizeClient(CancellationToken cancellationToken)
    {
        // При запуске авторизации необходимо знать chatId с клиентом.
        // Если вдруг он еще не известен, например, при первом запуске
        // то в настроках/конфиге можно его опустить, сделать NULL.
        // В таком случае при авторизации мы падаем в бесконечный Task где бот работает
        // в режиме прослушки. Он регаирует на любое сообщение и выдает в лог
        // chatId. 
        long clientChatId;
        //if (clientChatId is null)
        await ListenChatIds(cancellationToken);
        /*await _botClient.SendTextMessageAsync(
            chatId: clientChatId,
            text: "Авторизация клиента.",
            cancellationToken: cancellationToken);
        */
    }
}