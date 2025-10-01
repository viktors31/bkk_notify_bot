namespace BkkNotifyService.Options;

public class AppOptions
{
    public const string APP = nameof(APP);

    public string Phone { get; set; } = string.Empty;
    public long ClientToBotPeerId { get; set; } = 0;
    public long ListenPeerId { get; set; } = 0;
    public List<long> ReceivingChatsIds { get; set; } = new();
}