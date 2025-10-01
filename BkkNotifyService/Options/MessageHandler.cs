using BkkNotifyService.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TL;

namespace BkkNotifyService.Features;

public class MessageHandler : IHandler<MessageBase>
{
    private readonly WTelegram.Client _tgClient;
    private readonly ITelegramBotClient _botClient;
    private readonly IOptions<AppOptions> _options;

    public MessageHandler(WTelegram.Client tgClient, ITelegramBotClient botClient, IOptions<AppOptions> options)
    {
        _tgClient = tgClient;
        _botClient = botClient;
        _options = options;
    }

    public async Task Handle(MessageBase messageBase /*, CancellationToken cancellationToken*/)
    {
        //Здесь логика обработки сообщения
        if (messageBase is not TL.Message)
            return;
        TL.Message m = messageBase as TL.Message;
        Console.WriteLine($"{m.from_id} in {m.peer_id.ID}> {m.message}");
        //проверка что сообщение от овена
        if (m.peer_id.ID != _options.Value.ListenPeerId)
            return;
        //проверка что сообщение про событие
        if (!(m.message.StartsWith("Зарегистрировано начало события") ||
              m.message.StartsWith("Зарегистрировано окончание события")))
            return;
        //Строки с личными данными подлежащие удалению
        string[] toRemove = new[]
        {
            "Компания:",
            "Уведомление пользователя:",
            "Условие регистрации:",
        };
        //Разбиваем сообщение на массив строк построчно
        IEnumerable<string> toSendSplit = m.message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(mLine => !toRemove.Any(key => mLine.StartsWith(key)));
        //Добавляем жирный текст. Обработка текста в массиве строк
        IEnumerable<string> toSendBold = toSendSplit.Select(str =>
            {
                int index = str.IndexOf(":");
                str = str.Insert(0, "<b>");
                if (index == -1)
                    str = str + "</b>\n"; //Если нет двоеточия всю строку сделать жирной
                else
                    str = str.Insert(index + 3, "</b>"); //Если есть то только первое слово делаем
                return str;
            }
        );
        //Собираем массив строк обратно в одну строку для отправки
        string toSend = string.Join("\n", toSendBold);
        
        //Рассылка в чаты
        if (!_options.Value.ReceivingChatsIds.Any()) return;
        foreach (long chatId in _options.Value.ReceivingChatsIds)
        {
            await _botClient.SendMessage(chatId, toSend, ParseMode.Html);
            Thread.Sleep(1000); //Правила телеграма чтобы не попал в бан
        }
    }
   }