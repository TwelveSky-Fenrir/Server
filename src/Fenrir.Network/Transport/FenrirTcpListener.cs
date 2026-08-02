using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport.Logging;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

public sealed class FenrirTcpListener<TSession> : IAsyncDisposable
{
    private const int Backlog = 512;
    private readonly bool _applyOsSocketBuffers;
    private readonly Socket _listenSocket;

    private readonly ILogger? _logger;
    private readonly Func<long, IDuplexPipe, IPEndPoint?, TSession> _sessionFactory;
    private readonly SessionIdAllocator _sessionIds;

    public FenrirTcpListener(IPEndPoint endpoint, Func<long, IDuplexPipe, IPEndPoint?, TSession> sessionFactory,
        ILogger? logger = null, SessionIdAllocator? sessionIds = null, bool applyOsSocketBuffers = false)
    {
        _sessionFactory = sessionFactory;
        _logger = logger;
        _applyOsSocketBuffers = applyOsSocketBuffers;

        _sessionIds = sessionIds ?? SessionIdAllocator.Shared;

        _listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(endpoint);
        _listenSocket.Listen(Backlog);
    }

    public ValueTask DisposeAsync()
    {
        _listenSocket.Dispose();
        return ValueTask.CompletedTask;
    }

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
                break;
            }
            catch (SocketException ex)
            {
                _logger?.AcceptPortScanSwallowed(ex, _listenSocket.LocalEndPoint);
                continue;
            }

            SocketConnection? connection = null;
            try
            {
                connection = new SocketConnection(accepted, _logger, _applyOsSocketBuffers);
                var sessionId = _sessionIds.Next();
                var session = _sessionFactory(sessionId, connection, connection.RemoteEndPoint);

                _ = RunAcceptedAsync(session, connection, onAccepted, cancellationToken);
            }
            catch (Exception ex)
            {
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
        }
    }
}
