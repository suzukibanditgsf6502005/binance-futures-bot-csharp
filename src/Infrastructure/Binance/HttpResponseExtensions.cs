using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Binance;

public static class HttpResponseExtensions
{
    public static async Task EnsureSuccessOrThrowAsync(this HttpResponseMessage resp, CancellationToken ct = default)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = resp.Content != null ? await resp.Content.ReadAsStringAsync(ct) : string.Empty;
        throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase} | {body}");
    }
}
