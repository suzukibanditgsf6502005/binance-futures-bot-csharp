using System.Net.Http;
using Application;

namespace Infrastructure;

public class TelegramAlertService : IAlertService
{
    private readonly HttpClient _http = new();
    private readonly string _token;
    private readonly string _chatId;

    public TelegramAlertService(string token, string chatId)
    {
        _token = token;
        _chatId = chatId;
    }

    public async Task SendAsync(string message)
    {
        var url = $"https://api.telegram.org/bot{_token}/sendMessage";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = _chatId,
            ["text"] = message
        });
        try
        {
            await _http.PostAsync(url, content);
        }
        catch
        {
            // ignore alert failures
        }
    }
}

public class NoopAlertService : IAlertService
{
    public Task SendAsync(string message) => Task.CompletedTask;
}
