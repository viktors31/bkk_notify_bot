namespace BkkNotifyService.Options;

public class TelegramOptions
{
    public const string TELEGRAM = nameof(TELEGRAM);

    public string Token { get; set; } = string.Empty;
}