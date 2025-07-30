namespace BkkNotifyService.Options;

public class AppOptions
{
    public const string APP = nameof(APP);

    public long ClientChatId { get; set; } = 0;
    public List<long> ReceivingChatsIds { get; set; } = new();
    public string Phone { get; set; } = string.Empty;
}