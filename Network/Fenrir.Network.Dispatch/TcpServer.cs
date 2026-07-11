using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

public sealed class TcpServer<TSession>(
    IPEndPoint endpoint,
    Func<long, IDuplexPipe, IPEndPoint?, TSession> sessionFactory,
    IFrameDispatcher dispatcher,
    IOpcodeFrameSizeProvider registry,
    ISessionRateLimiter? rateLimiter = null,
    IpFloodGuard? ipFloodGuard = null,
    ILogger? logger = null) : IAsyncDisposable
    where TSession : ClientSession
{
    private readonly FenrirTcpListener<TSession> _listener = new(endpoint, sessionFactory, logger);

    public ValueTask DisposeAsync()
    {
        return _listener.DisposeAsync();
    }

    public Task AcceptLoopAsync(
        Func<TSession, SocketConnection, CancellationToken, Task> onAccepted,
        CancellationToken cancellationToken)
    {
        return _listener.AcceptLoopAsync(onAccepted, cancellationToken);
    }

    public Task RunSessionAsync(SocketConnection connection, TSession session, CancellationToken cancellationToken)
    {
        return SessionLoop.RunConnectionAsync(connection, session, dispatcher, registry, rateLimiter, ipFloodGuard,
            cancellationToken, logger);
    }
}
