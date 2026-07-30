using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Fenrir.Security.Abstractions;
using Fenrir.Security.FloodProtection;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

public sealed class TcpServer<TSession>(
    IPEndPoint endpoint,
    Func<long, IDuplexPipe, IPEndPoint?, TSession> sessionFactory,
    IFrameDispatcher dispatcher,
    IOpcodeFrameSizeProvider registry,
    ISessionRateLimiter? rateLimiter = null,
    IpFloodGuard? ipFloodGuard = null,
    ILogger? logger = null,
    SessionIdAllocator? sessionIds = null) : IAsyncDisposable
    where TSession : ClientSession
{
    private readonly FenrirTcpListener<TSession> _listener = new(endpoint, sessionFactory, logger, sessionIds);

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
