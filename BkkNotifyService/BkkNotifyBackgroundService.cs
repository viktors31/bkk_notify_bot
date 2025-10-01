using System.Text.RegularExpressions;
using BkkNotifyService.Features;
using BkkNotifyService.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TL;
using Update = Telegram.Bot.Types.Update;

namespace BkkNotifyService;

public class BkkNotifyBackgroundService : BackgroundService
{
    private readonly IOptions<AppOptions> _options;
    private readonly WTelegram.Client _tgClient;
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BkkNotifyBackgroundService> _logger;
    
    private readonly int _receiveVerifyCodeDelay = 60000;

    public BkkNotifyBackgroundService(
        IOptions<AppOptions> options,
        WTelegram.Client tgClient,
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        ILogger<BkkNotifyBackgroundService> logger)
    {
        _options = options;
        _tgClient = tgClient;
        _botClient = botClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BKK Notify Service v1.0.0 started at: {time}", DateTimeOffset.Now);
        await _botClient.DropPendingUpdates(); //скинуть необработанные события при запуске
        
        // При запуске авторизации необходимо знать chatId с клиентом.
        // Если вдруг он еще не известен, например, при первом запуске
        // то в настроках/конфиге можно его опустить, поставить значение 0.
        // В таком случае при авторизации мы падаем в бесконечный Task где бот работает
        // в режиме прослушки. Он регаирует на любое сообщение и выдает в лог
        // chatId. 
        if (_options.Value.ClientToBotPeerId == 0)
        {
            await ListenChatIds(stoppingToken);
            return;
        }

        if (!(await AuthorizeClient(stoppingToken)))
        {
            await _botClient.SendMessage(
                chatId: _options.Value.ClientToBotPeerId,
                text: $"<b>Авторизация клиента не прошла.</b>",
                ParseMode.Html,
                cancellationToken: stoppingToken);
            return;
        }
        await _botClient.SendMessage(
            chatId: _options.Value.ClientToBotPeerId,
            text: $"<b>Авторизация клиента УСПЕШНО.</b>",
            ParseMode.Html,
            cancellationToken: stoppingToken);
        var manager = _tgClient.WithUpdateManager(Client_UpdateHandler);
        while (!stoppingToken.IsCancellationRequested) {}
    }

    private async Task Client_UpdateHandler(TL.Update update)
    {
        var scope = _scopeFactory.CreateScope();
        var messageHandler = scope.ServiceProvider.GetRequiredService<IHandler<MessageBase>>();
        //note: switch с нследниками update делается через type-pattern.
        var handler = update switch
        {
            TL.UpdateNewMessage unm => messageHandler.Handle(unm.message),
            _ => UnknownUpdateHandlerAsync(update)
        };
        await handler;
    }
    
    private Task UnknownUpdateHandlerAsync(TL.Update update)
    {
        _logger.LogInformation("Unknown type message");
        return Task.CompletedTask;
    }

    private async Task ListenChatIds(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Сервис запущен в режиме прослушки chatId.");
        int? offset = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var update = await _botClient.GetUpdates(offset: offset ?? -1, timeout: 1, limit: 1, allowedUpdates: [UpdateType.Message]);
            if (update.Any())
            {
                var msg = update[0].Message;
                _logger.LogInformation($"Received {msg.Chat}: {msg.Text}");
                offset = update[0].Id + 1;
            }
        }
    }

    private async Task<bool> AuthorizeClient(CancellationToken cancellationToken)
    {
        string ip = await this.GetIp(); // достает внешний ip
        await _botClient.SendMessage(
            chatId: _options.Value.ClientToBotPeerId,
            text: $"<b>Авторизация клиента</b>\nПриложение запущено с IP: {ip}",
            ParseMode.Html,
            cancellationToken: cancellationToken);
        string requiredParam = await _tgClient.Login(_options.Value.Phone); // попытка залогиниться
        if (requiredParam == "verification_code") // если требует код то отловить и повторный вызов логина
        {
            _logger.LogInformation($"Client needs to receive verification code!");
            CancellationTokenSource receiveVerifyCodeTaskCancellationToken = new CancellationTokenSource();
            receiveVerifyCodeTaskCancellationToken.CancelAfter(_receiveVerifyCodeDelay);
            string verifyCode = await ReceiveVerifyCode(receiveVerifyCodeTaskCancellationToken.Token);
            if (verifyCode != string.Empty)
                requiredParam = await _tgClient.Login(verifyCode); //Повторный вызов с передачей кода
        }

        if (requiredParam is null)
        {
            _logger.LogInformation("Успешная авторизация.");
            return true;
        }
        _logger.LogInformation("Авторизация НЕ прошла!");
        return false;
    }
    
    private Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ApiRequestException apiRequestException:
                _logger.LogError(
                    apiRequestException,
                    "Telegram API Error:\n[{errorCode}]\n{message}",
                    apiRequestException.ErrorCode,
                    apiRequestException.Message);
                return Task.CompletedTask;

            default:
                _logger.LogError(exception, "Error while processing message in telegram bot");
                return Task.CompletedTask;
        }
    }

    public async Task<string> ReceiveVerifyCode(CancellationToken cancellationToken)
    {
        int? offset = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var update = await _botClient.GetUpdates(offset: offset ?? -1, timeout: 1, limit: 1, allowedUpdates: [UpdateType.Message]);
            if (update.Any())
            {
                var msg = update[0].Message;
                if ((msg.Chat.Id != _options.Value.ClientToBotPeerId) &&
                    (Regex.IsMatch(msg.Text, "[0-9]{5}", RegexOptions.IgnoreCase)))
                    return msg.Text;
                offset = update[0].Id + 1;
            }
            if (cancellationToken.IsCancellationRequested) break;
        }
        return string.Empty;
    }
}