namespace BkkNotifyService.Options;

public static class IpGetterExtension
{
    public static async Task<string> GetIp(this BkkNotifyBackgroundService service)
    {
        HttpClient httpClient = new HttpClient();
        string ipAddr = await httpClient.GetStringAsync("https://api.ipify.org");
        httpClient.Dispose();
        return ipAddr;
    }
}