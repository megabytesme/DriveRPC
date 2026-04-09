using System.Text.Json;

namespace DriveRPC.Android.Services;

internal sealed class GithubUpdateChecker
{
    public async Task<(bool updateAvailable, string version, string url, string body)> CheckAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "DriveRPC-Android");

        using var stream = await client.GetStreamAsync("https://api.github.com/repos/megabytesme/DriveRPC/releases");
        using var json = await JsonDocument.ParseAsync(stream);
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean())
            {
                continue;
            }

            var tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
            return (
                true,
                tag,
                release.GetProperty("html_url").GetString() ?? string.Empty,
                release.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty);
        }

        return (false, string.Empty, string.Empty, string.Empty);
    }
}
