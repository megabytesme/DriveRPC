using System.Net.Http.Headers;
using DriveRPC.Shared.Services;
using UserPresenceRPC.Discord.Net.Interfaces;

namespace DriveRPC.Android.Services;

internal sealed class AndroidWebHttpHandler : IHttpHandler
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    public void SetHeader(string name, string value)
    {
        _headers[name] = value;
    }

    public async Task<DiscordHttpResponse> GetAsync(string url, string userToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(request, userToken);

        using var response = await Client.SendAsync(request);
        return new DiscordHttpResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = await response.Content.ReadAsStringAsync()
        };
    }

    public async Task<DiscordHttpResponse> PostJsonAsync(string url, string json, string userToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json ?? string.Empty)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        ApplyHeaders(request, userToken);

        using var response = await Client.SendAsync(request);
        return new DiscordHttpResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = await response.Content.ReadAsStringAsync()
        };
    }

    public void Dispose()
    {
    }

    private void ApplyHeaders(HttpRequestMessage request, string? userToken)
    {
        foreach (var header in _headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrWhiteSpace(userToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", userToken);
        }
    }
}
