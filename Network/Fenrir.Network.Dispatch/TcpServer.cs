using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

/// <summary>
///     Encapsulates a listen socket's whole lifecycle -- <see cref="FenrirTcpListener{TSession}" />'s accept loop
///     plus, per accepted connection, <see cref="SessionLoop.RunConnectionAsync" /> -- as one cohesive object,
///     instead of a connection host wiring the two together by hand and threading <paramref name="dispatcher" />/
///     <paramref name="registry" />/<paramref name="rateLimiter" />/<paramref name="ipFloodGuard" />/
///     <paramref name="logger" /> through every accepted-connection call site itself. Knows nothing about Login
///     vs Zone -- that comes entirely from <typeparamref name="TSession" /> and the constructor arguments a
///     caller supplies.
/// </summary>
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
