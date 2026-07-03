using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Sessions;

namespace Fenrir.Network.Transport;

/// <summary>
///     Owns the listening socket for one server (Login or Zone) and turns each accepted TCP connection into a
///     (<see cref="ClientSession" />, <see cref="SocketConnection" />) pair. Deliberately unaware of
///     <see cref="Dispatching.SessionLoop" /> or any session registry — wiring those together (and tearing them
///     down together) is <c>onAccepted</c>'s job, supplied per-server by the Application layer.
/// </summary>
public sealed class FenrirTcpListener : IAsyncDisposable
{
    private const int Backlog = 512;

    private readonly Socket _listenSocket;
    private readonly Func<long, IDuplexPipe, IPEndPoint?, ClientSession> _sessionFactory;
    private long _nextSessionId;

    public FenrirTcpListener(IPEndPoint endpoint, Func<long, IDuplexPipe, IPEndPoint?, ClientSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;

        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(Backlog);
    }

    public ValueTask DisposeAsync()
    {
        _listenSocket.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Accepts connections until <paramref name="cancellationToken" /> fires or the listen socket is disposed.
    ///     Each acceptance is handed to <paramref name="onAccepted" /> as a detached task: a connection whose
    ///     handling runs for the connection's whole lifetime (<c>RunIOAsync</c> + <c>SessionLoop.RunAsync</c>) must
    ///     never stall accepting the next one, and any exception it throws must never bring the accept loop down.
    /// </summary>
    public async Task AcceptLoopAsync(
        Func<ClientSession, SocketConnection, CancellationToken, Task> onAccepted,
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
            catch (SocketException)
            {
                // Per Socket.AcceptAsync's own docs, a half-open port-scan (SYN -> SYN-ACK -> RST) can surface as a
                // per-accept SocketException with no connection ever accepted; only the listen socket itself being
                // torn down (ObjectDisposedException, above) should stop this loop.
                continue;
            }

            SocketConnection? connection = null;
            try
            {
                connection = new SocketConnection(accepted);
                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var session = _sessionFactory(sessionId, connection, connection.RemoteEndPoint);

                _ = RunAcceptedAsync(session, connection, onAccepted, cancellationToken);
            }
            catch (Exception)
            {
                // sessionFactory (or the SocketConnection construction above it) failing must not take the whole
                // accept loop down with it, nor leak the socket it was about to own — same never-goes-down
                // contract as onAccepted's, enforced the same way (swallow here, nothing else to do with it).
                if (connection is not null)
                    await connection.DisposeAsync().ConfigureAwait(false);
                else
                    accepted.Dispose();
            }
        }
    }

    private static async Task RunAcceptedAsync(
        ClientSession session,
        SocketConnection connection,
        Func<ClientSession, SocketConnection, CancellationToken, Task> onAccepted,
        CancellationToken cancellationToken)
    {
        try
        {
            await onAccepted(session, connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallowed by design (see AcceptLoopAsync's doc): onAccepted owns this connection's teardown and
            // logging end to end, so there is nothing more actionable to do with its exception here.
        }
    }
}
