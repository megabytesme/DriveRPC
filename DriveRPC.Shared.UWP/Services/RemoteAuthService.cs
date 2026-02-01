using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DriveRPC.Shared.UWP.Services
{
    public class RemoteAuthService : IDisposable
    {
        private MessageWebSocket _ws;
        private AsymmetricKeyAlgorithmProvider _rsaProvider;
        private CryptographicKey _keyPair;
        private Timer _heartbeatTimer;
        private readonly HttpClient _httpClient = new HttpClient();

        public event EventHandler<string> QrCodeUrlGenerated;
        public event EventHandler<string> UserDetected;
        public event EventHandler<string> TokenReceived;
        public event EventHandler<string> ErrorOccurred;

        public async Task InitializeAsync()
        {
            try
            {
                _rsaProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaOaepSha256);
                _keyPair = _rsaProvider.CreateKeyPair(2048);

                _ws = new MessageWebSocket();
                _ws.Control.MessageType = SocketMessageType.Utf8;
                _ws.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveRPC/1.0");
                _ws.SetRequestHeader("Origin", "https://discord.com");

                _ws.MessageReceived += OnMessageReceived;
                _ws.Closed += (s, e) => StopHeartbeat();

                await _ws.ConnectAsync(new Uri("wss://remote-auth-gateway.discord.gg/?v=2"));
            }
            catch (Exception ex) { ErrorOccurred?.Invoke(this, ex.Message); }
        }

        private async void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    string message = reader.ReadString(reader.UnconsumedBufferLength);
                    var data = JObject.Parse(message);
                    string op = data["op"]?.ToString();

                    switch (op)
                    {
                        case "hello":
                            StartHeartbeat((int)data["heartbeat_interval"]);
                            await SendInitAsync();
                            break;
                        case "nonce_proof":
                            await HandleNonceProofAsync(data["encrypted_nonce"].ToString());
                            break;
                        case "pending_remote_init":
                            QrCodeUrlGenerated?.Invoke(this, $"https://discord.com/ra/{data["fingerprint"]}");
                            break;
                        case "pending_ticket":
                            await HandlePendingTicketAsync(data["encrypted_user_payload"].ToString());
                            break;
                        case "pending_login":
                            await FetchTokenAsync(data["ticket"].ToString());
                            break;
                        case "cancel":
                            ErrorOccurred?.Invoke(this, "Cancelled");
                            break;
                    }
                }
            }
            catch { }
        }

        private async Task SendInitAsync()
        {
            var publicKey = _keyPair.ExportPublicKey(CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo);
            await SendRawAsync(JsonConvert.SerializeObject(new
            {
                op = "init",
                encoded_public_key = CryptographicBuffer.EncodeToBase64String(publicKey)
            }));
        }

        private async Task HandleNonceProofAsync(string encryptedNonce)
        {
            var decrypted = CryptographicEngine.Decrypt(_keyPair, CryptographicBuffer.DecodeFromBase64String(encryptedNonce), null);
            string proof = CryptographicBuffer.EncodeToBase64String(decrypted).Replace('/', '_').Replace('+', '-').Replace("=", "");
            await SendRawAsync(JsonConvert.SerializeObject(new { op = "nonce_proof", nonce = proof }));
        }

        private async Task HandlePendingTicketAsync(string encryptedPayload)
        {
            try
            {
                var decrypted = CryptographicEngine.Decrypt(_keyPair, CryptographicBuffer.DecodeFromBase64String(encryptedPayload), null);
                var parts = Encoding.UTF8.GetString(decrypted.ToArray()).Split(':');
                UserDetected?.Invoke(this, parts.Length >= 4 ? parts[3] : "User");
            }
            catch { }
        }

        private async Task FetchTokenAsync(string ticket)
        {
            var response = await _httpClient.PostAsync("https://discord.com/api/v9/users/@me/remote-auth/login",
                new StringContent(JsonConvert.SerializeObject(new { ticket }), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                var decrypted = CryptographicEngine.Decrypt(_keyPair, CryptographicBuffer.DecodeFromBase64String(result["encrypted_token"].ToString()), null);
                TokenReceived?.Invoke(this, Encoding.UTF8.GetString(decrypted.ToArray()).Trim('"'));
            }
        }

        private void StartHeartbeat(int intervalMs)
        {
            _heartbeatTimer = new Timer(async _ => await SendRawAsync(JsonConvert.SerializeObject(new { op = "heartbeat" })), null, intervalMs, intervalMs);
        }

        private void StopHeartbeat() => _heartbeatTimer?.Dispose();

        private async Task SendRawAsync(string message)
        {
            if (_ws == null) return;
            try
            {
                using (var writer = new DataWriter(_ws.OutputStream))
                {
                    writer.WriteString(message);
                    await writer.StoreAsync();
                    writer.DetachStream();
                }
            }
            catch { }
        }

        public void Dispose() { StopHeartbeat(); _ws?.Dispose(); _httpClient?.Dispose(); }
    }
}