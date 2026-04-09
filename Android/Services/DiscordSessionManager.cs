using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Widget;
using AndroidX.AppCompat.App;
using DriveRPC.Shared.Models;
using DriveRPC.Shared.Services;
using Newtonsoft.Json;
using UserPresenceRPC.Discord.Net.Services;
using ZXing;
using ZXing.Common;

namespace DriveRPC.Android.Services;

internal sealed class DiscordSessionManager
{
    private readonly ISecureStorage _secureStorage;

    public DiscordSessionManager(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public Task<string> LoadTokenAsync()
        => _secureStorage.LoadAsync(SecureStorageKeys.UserToken);

    public Task SaveTokenAsync(string token)
        => _secureStorage.SaveAsync(SecureStorageKeys.UserToken, token);

    public Task ClearTokenAsync()
        => _secureStorage.DeleteAsync(SecureStorageKeys.UserToken);

    public async Task<DiscordUser?> LoadUserAsync()
    {
        var token = await LoadTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Linux; Android 15) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Mobile Safari/537.36");

        try
        {
            var response = await client.GetStringAsync("https://discord.com/api/v9/users/@me");
            return JsonConvert.DeserializeObject<DiscordUser>(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task ShowTokenDialogAsync(Activity activity, Func<Task> onChanged, Func<Task<bool>> verifySavedTokenAccessAsync)
    {
        var savedToken = await LoadTokenAsync();
        var isSignedIn = !string.IsNullOrWhiteSpace(savedToken);
        var container = new LinearLayout(activity)
        {
            Orientation = Orientation.Vertical
        };
        container.SetPadding(48, 32, 48, 24);

        var tokenInput = new EditText(activity)
        {
            Hint = "Enter Discord token"
        };
        tokenInput.InputType = global::Android.Text.InputTypes.ClassText | global::Android.Text.InputTypes.TextVariationPassword;
        tokenInput.Text = isSignedIn ? string.Empty : savedToken ?? string.Empty;
        container.AddView(tokenInput);

        var revealToggle = new CheckBox(activity)
        {
            Text = "Show token text"
        };
        revealToggle.CheckedChange += (_, args) =>
        {
            tokenInput.InputType = global::Android.Text.InputTypes.ClassText |
                (args.IsChecked
                    ? global::Android.Text.InputTypes.TextVariationVisiblePassword
                    : global::Android.Text.InputTypes.TextVariationPassword);
            tokenInput.SetSelection(tokenInput.Text?.Length ?? 0);
        };
        revealToggle.Enabled = !isSignedIn;
        container.AddView(revealToggle);

        var infoText = new TextView(activity)
        {
            Text = isSignedIn
                ? "Your saved token is hidden. Unlock to reveal it, or paste a replacement token below."
                : "Use manual token entry or open QR login.",
            TextSize = 12
        };
        container.AddView(infoText);

        var buttonPanel = new LinearLayout(activity)
        {
            Orientation = Orientation.Vertical
        };
        buttonPanel.SetPadding(0, 24, 0, 0);

        AndroidX.AppCompat.App.AlertDialog? dialog = null;

        if (isSignedIn)
        {
            var revealButton = new Button(activity) { Text = "View Saved Token" };
            revealButton.Click += async (_, _) =>
            {
                var verified = await verifySavedTokenAccessAsync();
                if (!verified)
                {
                    infoText.Text = "Unlock failed or was canceled.";
                    return;
                }

                tokenInput.Text = savedToken ?? string.Empty;
                revealToggle.Enabled = true;
                infoText.Text = "Identity verified. You can reveal the saved token for this session.";
            };
            buttonPanel.AddView(revealButton);

            var saveButton = new Button(activity) { Text = "Save Changes" };
            saveButton.Click += async (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(tokenInput.Text))
                {
                    await SaveTokenAsync(tokenInput.Text!);
                    await onChanged();
                }

                dialog?.Dismiss();
            };
            buttonPanel.AddView(saveButton);

            var signOutButton = new Button(activity) { Text = "Sign Out" };
            signOutButton.Click += async (_, _) =>
            {
                await ClearTokenAsync();
                await onChanged();
                dialog?.Dismiss();
            };
            buttonPanel.AddView(signOutButton);
        }
        else
        {
            var saveButton = new Button(activity) { Text = "Save & Connect" };
            saveButton.Click += async (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(tokenInput.Text))
                {
                    await SaveTokenAsync(tokenInput.Text!);
                    await onChanged();
                    dialog?.Dismiss();
                }
            };
            buttonPanel.AddView(saveButton);

            var qrButton = new Button(activity) { Text = "QR Login" };
            qrButton.Click += async (_, _) =>
            {
                dialog?.Dismiss();
                await ShowQrLoginDialogAsync(activity, onChanged);
            };
            buttonPanel.AddView(qrButton);
        }

        container.AddView(buttonPanel);

        dialog = new AndroidX.AppCompat.App.AlertDialog.Builder(activity)
            .SetTitle(isSignedIn ? "Manage Discord account" : "Sign In")
            .SetView(container)
            .SetPositiveButton("Close", (_, _) => { })
            .Create();
        dialog.Show();
    }

    private async Task ShowQrLoginDialogAsync(Activity activity, Func<Task> onChanged)
    {
        var stack = new LinearLayout(activity)
        {
            Orientation = Orientation.Vertical
        };
        stack.SetPadding(48, 32, 48, 32);

        var statusText = new TextView(activity)
        {
            Text = "Connecting to Discord..."
        };
        stack.AddView(statusText);

        var image = new ImageView(activity);
        image.SetAdjustViewBounds(true);
        stack.AddView(image);

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        var socket = new AndroidClientWebSocketAdapter();
        var http = new AndroidWebHttpHandler();
        http.SetHeader(
            "User-Agent",
            "Mozilla/5.0 (Linux; Android 15) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Mobile Safari/537.36");
        http.SetHeader("Origin", "https://discord.com");
        var service = new RemoteAuthService(socket, http);

        service.QrCodeUrlGenerated += (_, url) =>
        {
            activity.RunOnUiThread(() =>
            {
                image.SetImageBitmap(GenerateQrCode(url));
                statusText.Text = "Scan this QR code in Discord.";
            });
        };
        service.UserDetected += (_, user) =>
        {
            activity.RunOnUiThread(() => statusText.Text = $"Confirm login for {user}...");
        };
        service.TokenReceived += async (_, token) =>
        {
            await SaveTokenAsync(token);
            activity.RunOnUiThread(async () =>
            {
                Toast.MakeText(activity, "Discord account connected.", ToastLength.Short)?.Show();
                dialog?.Dismiss();
                await onChanged();
            });
        };
        service.ErrorOccurred += (_, message) =>
        {
            activity.RunOnUiThread(() => statusText.Text = $"QR login failed: {message}");
        };

        dialog = new AndroidX.AppCompat.App.AlertDialog.Builder(activity)
            .SetTitle("Discord QR login")
            .SetView(stack)
            .SetPositiveButton("Close", (_, _) => { })
            .Create();

        dialog.DismissEvent += (_, _) =>
        {
            service.Dispose();
            socket.Dispose();
            http.Dispose();
        };

        dialog.Show();

        try
        {
            await service.InitializeAsync();
        }
        catch (Exception ex)
        {
            statusText.Text = ex.Message;
        }
    }

    private static Bitmap GenerateQrCode(string text)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Height = 512,
                Width = 512,
                Margin = 1
            }
        };

        var pixelData = writer.Write(text);
        var bitmap = Bitmap.CreateBitmap(pixelData.Width, pixelData.Height, Bitmap.Config.Argb8888!);
        bitmap.CopyPixelsFromBuffer(Java.Nio.ByteBuffer.Wrap(pixelData.Pixels));
        return bitmap;
    }
}
