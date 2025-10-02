namespace BkkNotifyService.Options;

public class TelegramOptions
{
    public const string TELEGRAM = nameof(TELEGRAM);

    public string Token { get; set; } = string.Empty;
    public int ApiId { get; set; } = 0;
    public string ApiHash { get; set; } = string.Empty;
    
    public string SessionPathname { get; set; } = string.Empty;
}