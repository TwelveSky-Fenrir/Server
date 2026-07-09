using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport.Logging;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

// Turns accepted TCP connections into (TSession, SocketConnection) pairs; wiring/teardown is onAccepted's job.
// Generic over the session type so Transport never needs to reference the concrete session hierarchy (Dispatch).
public sealed class FenrirTcpListener<TSession> : IAsyncDisposable
{
    private const int Backlog = 512;
    private readonly Socket _listenSocket;

    private readonly ILogger? _logger;
    private readonly Func<long, IDuplexPipe, IPEndPoint?, TSession> _sessionFactory;
    private long _nextSessionId;

    public FenrirTcpListener(IPEndPoint endpoint, Func<long, IDuplexPipe, IPEndPoint?, TSession> sessionFactory,
        ILogger? logger = null)
    {
        _sessionFactory = sessionFactory;
        _logger = logger;

        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(Backlog);
    }

    public ValueTask DisposeAsync()
    {
        _listenSocket.Dispose();
        return ValueTask.CompletedTask;
    }

    // Each acceptance runs as a detached task so a connection's whole lifetime never stalls the next accept.
    public async Task AcceptLoopAsync(
        Func<TSession, SocketConnection, CancellationToken, Task> onAccepted,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket accepted;
            try
            {
                accepted = await _listenSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                break; // cancelled, or Stop()/DisposeAsync() closed the listen socket
            }
            catch (SocketException ex)
            {
                // A half-open port-scan can surface as a per-accept SocketException with nothing accepted; only
                // the listen socket being torn down (ObjectDisposedException, above) should stop this loop.
                // Debug, not Warning -- see TransportLog.AcceptPortScanSwallowed's own remarks for why this is
                // expected noise, not an anomaly.
                _logger?.AcceptPortScanSwallowed(ex, _listenSocket.LocalEndPoint);
                continue;
            }

            SocketConnection? connection = null;
            try
            {
                connection = new SocketConnection(accepted, _logger);
                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var session = _sessionFactory(sessionId, connection, connection.RemoteEndPoint);

                _ = RunAcceptedAsync(session, connection, onAccepted, cancellationToken);
            }
            catch (Exception ex)
            {
                // Must not crash the accept loop or leak the socket; same never-goes-down contract as onAccepted's.
                // Unlike the SocketException above, this is a genuine anomaly (a successful accept that failed
                // to become a running session) -- see TransportLog.ConnectionConstructionFailed's own remarks.
                _logger?.ConnectionConstructionFailed(ex, _listenSocket.LocalEndPoint);

                if (connection is not null)
                    await connection.DisposeAsync().ConfigureAwait(false);
                else
                    accepted.Dispose();
            }
        }
    }

    private static async Task RunAcceptedAsync(
        TSession session,
        SocketConnection connection,
        Func<TSession, SocketConnection, CancellationToken, Task> onAccepted,
        CancellationToken cancellationToken)
    {
        try
        {
            await onAccepted(session, connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallowed by design: onAccepted owns this connection's teardown/logging end to end.
        }
    }
}
