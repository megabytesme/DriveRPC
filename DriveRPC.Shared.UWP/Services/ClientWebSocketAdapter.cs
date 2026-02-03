using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

namespace DriveRPC.Shared.UWP.Services
{
    public class ClientWebSocketAdapter : IWebSocketClient, IDisposable
    {
        private MessageWebSocket _socket;
        private DataWriter _writer;
        private RpcWebSocketState _state = RpcWebSocketState.None;

        public RpcWebSocketState State => _state;

        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        private readonly ConcurrentDictionary<string, string> _pendingHeaders =
            new ConcurrentDictionary<string, string>();

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            _socket = new MessageWebSocket();
            _socket.Control.MessageType = SocketMessageType.Utf8;

            _socket.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _socket.SetRequestHeader("Origin", "https://discord.com");

            foreach (var kv in _pendingHeaders)
                _socket.SetRequestHeader(kv.Key, kv.Value);

            _socket.MessageReceived += Socket_MessageReceived;
            _socket.Closed += Socket_Closed;

            _state = RpcWebSocketState.Connecting;

            try
            {
                Debug.WriteLine("[WS LOG] Starting ConnectAsync...");

                var connectTask = _socket.ConnectAsync(uri).AsTask(cancellationToken);
                var timeoutTask = Task.Delay(8000, cancellationToken);

                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == timeoutTask)
                {
                    Debug.WriteLine("[WS LOG] ConnectAsync timed out, retrying...");
                    _socket.Dispose();
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    await ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await connectTask.ConfigureAwait(false);

                _writer = new DataWriter(_socket.OutputStream);
                _state = RpcWebSocketState.Open;

                Debug.WriteLine("[WS LOG] Connected successfully to Auth Gateway.");
            }
            catch (Exception ex)
            {
                _state = RpcWebSocketState.Closed;
                Debug.WriteLine($"[WS LOG] Connect failed: {ex.Message}");
                throw;
            }
        }

        private void Socket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            _state = RpcWebSocketState.Closed;
            Debug.WriteLine($"[WS LOG] Socket closed: {args.Code} {args.Reason}");

            _signal.Release();
        }

        private void Socket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    var text = reader.ReadString(reader.UnconsumedBufferLength);

                    _queue.Enqueue(text);
                    _signal.Release();
                }
            }
            catch (Exception ex)
            {
                const uint WININET_E_CONNECTION_ABORTED = 0x80072EFE;
                if ((uint)ex.HResult == WININET_E_CONNECTION_ABORTED)
                    return;

                Debug.WriteLine($"[WS LOG] MessageReceived exception: {ex}");
            }
        }

        public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_queue.TryDequeue(out var message))
                    return message;

                if (_state == RpcWebSocketState.Closed)
                    return null;

                try
                {
                    await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private async Task<RpcWebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_queue.TryDequeue(out var message))
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    var count = Math.Min(bytes.Length, buffer.Count);
                    Array.Copy(bytes, 0, buffer.Array, buffer.Offset, count);

                    return new RpcWebSocketReceiveResult
                    {
                        Count = count,
                        EndOfMessage = true,
                        MessageType = RpcWebSocketMessageType.Text
                    };
                }

                if (_state == RpcWebSocketState.Closed)
                {
                    return new RpcWebSocketReceiveResult
                    {
                        Count = 0,
                        EndOfMessage = true,
                        MessageType = RpcWebSocketMessageType.Text
                    };
                }

                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<byte[]> ReceiveBytesAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var result = await ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

            if (result.Count == 0)
                return null;

            var data = new byte[result.Count];
            Array.Copy(buffer, 0, data, 0, result.Count);
            return data;
        }

        public async Task SendAsync(
            ArraySegment<byte> buffer,
            RpcWebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (_state != RpcWebSocketState.Open)
                return;

            try
            {
                var text = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                _writer.WriteString(text);
                await _writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _state = RpcWebSocketState.Closed;
                try { _writer?.DetachStream(); } catch { }
                try { _socket?.Dispose(); } catch { }
                throw;
            }
        }

        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            return SendAsync(new ArraySegment<byte>(bytes), RpcWebSocketMessageType.Text, true, cancellationToken);
        }

        public Task CloseAsync(
            RpcWebSocketCloseStatus closeStatus,
            string statusDescription,
            CancellationToken cancellationToken)
        {
            if (_socket != null)
            {
                _state = RpcWebSocketState.CloseSent;
                _socket.Close((ushort)closeStatus, statusDescription);
                _state = RpcWebSocketState.Closed;
                _signal.Release();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _writer?.DetachStream();
                _writer?.Dispose();
                _socket?.Dispose();
            }
            catch { }
        }

        public void SetHeader(string name, string value)
        {
            _pendingHeaders[name] = value;
        }
    }
}
