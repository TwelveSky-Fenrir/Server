using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

public class ClientBase : IClient
{
    private bool _disposed;
    private bool _disposing;

    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    
    // private readonly IMessageDecoder<TMessage> _messageDecoder;
    // private readonly IMessageDispatcher<TPacketType, TSessionData, TMessage> _messageDispatcher;
    // protected readonly IMessageEncoder<TMessage> _messageEncoder;
    
    // TODO: Should server options be here?
    // private readonly FenrirServerOptions _options;
    
    private Socket socket;
    
    protected readonly IDuplexPipe _pipe;
    private readonly Socket _socket;
    
    /// <summary>Gets the receive buffer for the session.</summary>
    public byte[] RecvBuffer { get; }

    public uint RecvBufferPosition { get; set; }

    // private string? _sessionId;
    public ISession? Session { get; set; }
    public Task ReceiveAsync()
    {
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposing = true;

        await CastAndDispose(_cts);
        await CastAndDispose(_socket);
        if (Session != null) await Session.DisposeAsync();
        
        _disposed = true;
        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}