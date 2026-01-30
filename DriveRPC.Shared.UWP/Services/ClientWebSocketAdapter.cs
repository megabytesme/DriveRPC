using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DriveRPC.Shared.UWP.Services
{
    public class ClientWebSocketAdapter : IWebSocketClient
    {
        private MessageWebSocket _socket;
        private DataWriter _writer;
        private RpcWebSocketState _state = RpcWebSocketState.None;

        public RpcWebSocketState State => _state;

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            _socket = new MessageWebSocket();
            _socket.Control.MessageType = SocketMessageType.Utf8;

            _socket.MessageReceived += Socket_MessageReceived;
            _socket.Closed += Socket_Closed;

            _writer = new DataWriter(_socket.OutputStream);

            _state = RpcWebSocketState.Connecting;
            await _socket.ConnectAsync(uri);
            _state = RpcWebSocketState.Open;
        }

        private void Socket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            _state = RpcWebSocketState.Closed;
        }

        private void Socket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            using (var reader = args.GetDataReader())
            {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                var text = reader.ReadString(reader.UnconsumedBufferLength);

                lock (_receiveBuffer)
                {
                    _receiveBuffer.Enqueue(text);
                }
            }
        }

        private readonly Queue<string> _receiveBuffer = new Queue<string>();

        public Task<RpcWebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            string message = null;

            lock (_receiveBuffer)
            {
                if (_receiveBuffer.Count > 0)
                    message = _receiveBuffer.Dequeue();
            }

            if (message == null)
            {
                return Task.FromResult(new RpcWebSocketReceiveResult
                {
                    Count = 0,
                    EndOfMessage = true,
                    MessageType = RpcWebSocketMessageType.Text
                });
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            Array.Copy(bytes, buffer.Array, bytes.Length);

            return Task.FromResult(new RpcWebSocketReceiveResult
            {
                Count = bytes.Length,
                EndOfMessage = true,
                MessageType = RpcWebSocketMessageType.Text
            });
        }

        public async Task SendAsync(
            ArraySegment<byte> buffer,
            RpcWebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (_state != RpcWebSocketState.Open)
                return;

            var text = System.Text.Encoding.UTF8.GetString(buffer.Array, 0, buffer.Count);
            _writer.WriteString(text);
            await _writer.StoreAsync();
        }

        public async Task CloseAsync(
            RpcWebSocketCloseStatus closeStatus,
            string statusDescription,
            CancellationToken cancellationToken)
        {
            if (_socket != null)
            {
                _state = RpcWebSocketState.CloseSent;
                _socket.Close((ushort)closeStatus, statusDescription);
                _state = RpcWebSocketState.Closed;
            }

            await Task.CompletedTask;
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
    }
}