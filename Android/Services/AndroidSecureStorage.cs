using Android.Content;
using Android.Preferences;
using DriveRPC.Shared.Services;

namespace DriveRPC.Android.Services;

internal sealed class AndroidSecureStorage : ISecureStorage
{
    private readonly ISharedPreferences _preferences;

    public AndroidSecureStorage(Context context)
    {
        _preferences = PreferenceManager.GetDefaultSharedPreferences(context)
            ?? throw new InvalidOperationException("Shared preferences are unavailable.");
    }

    public Task SaveAsync(string key, string value)
    {
        _preferences.Edit()!.PutString(key, value)?.Apply();
        return Task.CompletedTask;
    }

    public Task<string?> LoadAsync(string key)
        => Task.FromResult(_preferences.GetString(key, null));

    public Task DeleteAsync(string key)
    {
        _preferences.Edit()!.Remove(key)?.Apply();
        return Task.CompletedTask;
    }
}
