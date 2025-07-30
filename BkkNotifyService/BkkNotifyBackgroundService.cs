using System.Text.RegularExpressions;
using BkkNotifyService.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BkkNotifyService;

public class BkkNotifyBackgroundService : BackgroundService
{
    private readonly IOptions<AppOptions> _options;
    private readonly WTelegram.Client _tgClient;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<BkkNotifyBackgroundService> _logger;

    private readonly int ReceiveVerifyCodeDelay = 60000;

    public BkkNotifyBackgroundService(
        IOptions<AppOptions> options,
        WTelegram.Client tgClient,
        ITelegramBotClient botClient,
        ILogger<BkkNotifyBackgroundService> logger)
    {
        _options = options;
        _tgClient = tgClient;
        _botClient = botClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BKK Notify Service v1.0.0 started at: {time}", DateTimeOffset.Now);
        await _botClient.DropPendingUpdates(); //скинуть необработанные события при запуске
        if (!(await AuthorizeClient(stoppingToken)))
        {
            await _botClient.SendMessage(
                chatId: _options.Value.ClientChatId,
                text: $"<b>Авторизация клиента не прошла.</b>",
                ParseMode.Html,
                cancellationToken: stoppingToken);
            return;
        }
        await _botClient.SendMessage(
            chatId: _options.Value.ClientChatId,
            text: $"<b>Авторизация клиента УСПЕШНО.</b>",
            ParseMode.Html,
            cancellationToken: stoppingToken);
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
                    await HandleErrorAsync(_botClient, ex, cancellationToken);
                }
            }
            if (cancellationToken.IsCancellationRequested) break;
        }
    }

    private async Task<bool> AuthorizeClient(CancellationToken cancellationToken)
    {
        // При запуске авторизации необходимо знать chatId с клиентом.
        // Если вдруг он еще не известен, например, при первом запуске
        // то в настроках/конфиге можно его опустить, поставить значение 0.
        // В таком случае при авторизации мы падаем в бесконечный Task где бот работает
        // в режиме прослушки. Он регаирует на любое сообщение и выдает в лог
        // chatId. 
        if (_options.Value.ClientChatId == 0)
            await ListenChatIds(cancellationToken);
        
        string ip = await GetIp(); // достает внешний ip
        await _botClient.SendMessage(
            chatId: _options.Value.ClientChatId,
            text: $"<b>Авторизация клиента</b>\nПриложение запущено с IP: {ip}",
            ParseMode.Html,
            cancellationToken: cancellationToken);
        string requiredParam = await _tgClient.Login(_options.Value.Phone); // попытка залогиниться
        if (requiredParam == "verification_code") // если требует код то отловить и повторный вызов логина
        {
            _logger.LogInformation($"Client needs to receive verification code!");
            CancellationTokenSource receiveCodeTaskCancellationToken = new CancellationTokenSource();
            receiveCodeTaskCancellationToken.CancelAfter(ReceiveVerifyCodeDelay);
            string verificationCode = await ReceiveVerifyCode(receiveCodeTaskCancellationToken.Token);
            if (verificationCode != string.Empty)
                requiredParam = await _tgClient.Login(verificationCode); //Повторный вызов с передачей кода
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
            var updates = await _botClient.GetUpdates(offset, timeout: 2);
            foreach (var update in updates)
            {
                offset = update.Id + 1;
                try
                {
                    switch (update)
                    {
                        case { Message: { } msg }:
                            if ((msg.Chat.Id != _options.Value.ClientChatId) &&
                                (Regex.IsMatch(msg.Text, "[0-9]{5}", RegexOptions.IgnoreCase)))
                            {
                                _logger.LogInformation($"Received {msg.Chat}: {msg.Text}");
                                return msg.Text;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await HandleErrorAsync(_botClient, ex, cancellationToken);
                }
            }
            if (cancellationToken.IsCancellationRequested) break;
        }
        return string.Empty;
    }
    
    public static async Task<string> GetIp()
    {
        HttpClient httpClient = new HttpClient();
        string ipAddr = await httpClient.GetStringAsync("https://api.ipify.org");
        httpClient.Dispose();
        return ipAddr;
    }
}