using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DriveRPC.Shared.Services
{
    public class RemoteAuthService : IDisposable
    {
        private ClientWebSocket _ws;
        private RSA _rsa;
        private Timer _heartbeatTimer;
        private readonly HttpClient _httpClient = new HttpClient();
        private CancellationTokenSource _cts;

        public event EventHandler<string> QrCodeUrlGenerated;
        public event EventHandler<string> UserDetected;
        public event EventHandler<string> TokenReceived;
        public event EventHandler<string> ErrorOccurred;

        public async Task InitializeAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _rsa = RSA.Create();
                _rsa.KeySize = 2048;

                _ws = new ClientWebSocket();
                try
                {
                    _ws.Options.SetRequestHeader("Origin", "https://discord.com");
                    _ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriveRPC/1.0");
                }
                catch { }

                await _ws.ConnectAsync(new Uri("wss://remote-auth-gateway.discord.gg/?v=2"), _cts.Token);
                _ = ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024 * 16];
            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
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
                            _ = FetchTokenAsync(data["ticket"].ToString());
                            break;
                        case "cancel":
                            ErrorOccurred?.Invoke(this, "Cancelled");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_ws.State != WebSocketState.Aborted && _ws.State != WebSocketState.Closed && !_cts.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(this, ex.Message);
                }
            }
        }

        private async Task SendInitAsync()
        {
            try
            {
                RSAParameters rp = _rsa.ExportParameters(false);
                byte[] publicKey = ExportRsaPublicKey(rp.Modulus, rp.Exponent);
                await SendRawAsync(JsonConvert.SerializeObject(new
                {
                    op = "init",
                    encoded_public_key = Convert.ToBase64String(publicKey)
                }));
            }
            catch { }
        }

        private async Task HandleNonceProofAsync(string encryptedNonce)
        {
            try
            {
                byte[] decrypted = _rsa.Decrypt(Convert.FromBase64String(encryptedNonce), RSAEncryptionPadding.OaepSHA256);
                string proof = Convert.ToBase64String(decrypted).Replace('/', '_').Replace('+', '-').Replace("=", "");
                await SendRawAsync(JsonConvert.SerializeObject(new { op = "nonce_proof", nonce = proof }));
            }
            catch { }
        }

        private async Task HandlePendingTicketAsync(string encryptedPayload)
        {
            try
            {
                byte[] decrypted = _rsa.Decrypt(Convert.FromBase64String(encryptedPayload), RSAEncryptionPadding.OaepSHA256);
                var parts = Encoding.UTF8.GetString(decrypted).Split(':');
                UserDetected?.Invoke(this, parts.Length >= 4 ? parts[3] : "User");
            }
            catch { }
        }

        private async Task FetchTokenAsync(string ticket)
        {
            try
            {
                var response = await _httpClient.PostAsync("https://discord.com/api/v9/users/@me/remote-auth/login",
                    new StringContent(JsonConvert.SerializeObject(new { ticket }), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var result = JObject.Parse(await response.Content.ReadAsStringAsync());
                    byte[] decrypted = _rsa.Decrypt(Convert.FromBase64String(result["encrypted_token"].ToString()), RSAEncryptionPadding.OaepSHA256);
                    TokenReceived?.Invoke(this, Encoding.UTF8.GetString(decrypted).Trim('"'));
                }
            }
            catch { }
        }

        private void StartHeartbeat(int intervalMs)
        {
            _heartbeatTimer = new Timer(async _ => await SendRawAsync(JsonConvert.SerializeObject(new { op = "heartbeat" })), null, intervalMs, intervalMs);
        }

        private async Task SendRawAsync(string message)
        {
            if (_ws?.State != WebSocketState.Open) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch { }
        }

        private byte[] ExportRsaPublicKey(byte[] modulus, byte[] exponent)
        {
            byte[] modulusPrefix = modulus[0] >= 0x80 ? new byte[] { 0x02, 0x82, 0x01, 0x01, 0x00 } : new byte[] { 0x02, 0x82, 0x01, 0x01 };
            byte[] exponentPart = new byte[] { 0x02, (byte)exponent.Length };
            byte[] rsaKeySeq = Combine(modulusPrefix, modulus, exponentPart, exponent);
            byte[] innerSeq = Combine(new byte[] { 0x30, 0x82, 0x01, 0x0A }, rsaKeySeq);
            byte[] bitString = Combine(new byte[] { 0x03, 0x82, 0x01, 0x0F, 0x00 }, innerSeq);
            byte[] algorithmId = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            return Combine(new byte[] { 0x30, 0x82, 0x01, 0x22 }, algorithmId, bitString);
        }

        private byte[] Combine(params byte[][] arrays)
        {
            int len = 0;
            foreach (var a in arrays) len += a.Length;
            byte[] res = new byte[len];
            int offset = 0;
            foreach (var a in arrays) { Buffer.BlockCopy(a, 0, res, offset, a.Length); offset += a.Length; }
            return res;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _heartbeatTimer?.Dispose();
            _ws?.Dispose();
            _rsa?.Dispose();
            _httpClient?.Dispose();
        }
    }
}