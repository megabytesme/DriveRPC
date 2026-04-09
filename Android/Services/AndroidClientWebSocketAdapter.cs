using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using UserPresenceRPC.Discord.Net.Interfaces;

namespace DriveRPC.Android.Services;

internal sealed class AndroidClientWebSocketAdapter : IWebSocketClient, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _pendingHeaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private ClientWebSocket? _socket;
    private RpcWebSocketState _state = RpcWebSocketState.None;

    public RpcWebSocketState State => _state;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        _socket = new ClientWebSocket();
        foreach (var header in _pendingHeaders)
        {
            _socket.Options.SetRequestHeader(header.Key, header.Value);
        }

        _state = RpcWebSocketState.Connecting;
        await _socket.ConnectAsync(uri, cancellationToken);
        _state = RpcWebSocketState.Open;
        _ = Task.Run(() => ReceiveLoopAsync(_socket, cancellationToken), cancellationToken);
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_queue.TryDequeue(out var message))
            {
                return message;
            }

            if (_state == RpcWebSocketState.Closed)
            {
                return null;
            }

            await _signal.WaitAsync(cancellationToken);
        }
    }

    public async Task<byte[]?> ReceiveBytesAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var message = await ReceiveAsync(cancellationToken);
        if (message == null)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length));
        return bytes;
    }

    public async Task SendAsync(ArraySegment<byte> buffer, RpcWebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        if (_socket == null)
        {
            return;
        }

        await _socket.SendAsync(
            buffer,
            messageType == RpcWebSocketMessageType.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
            endOfMessage,
            cancellationToken);
    }

    public Task SendAsync(string message, CancellationToken cancellationToken)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        return SendAsync(buffer, RpcWebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task CloseAsync(RpcWebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        if (_socket == null)
        {
            return;
        }

        await _socket.CloseAsync((WebSocketCloseStatus)closeStatus, statusDescription, cancellationToken);
        _state = RpcWebSocketState.Closed;
        _signal.Release();
    }

    public void Dispose()
    {
        _socket?.Dispose();
        _signal.Dispose();
    }

    public void SetHeader(string name, string value)
    {
        _pendingHeaders[name] = value;
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _state = RpcWebSocketState.Closed;
                        _signal.Release();
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                _queue.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                _signal.Release();
            }
        }
        catch
        {
            _state = RpcWebSocketState.Closed;
            _signal.Release();
        }
    }
}
