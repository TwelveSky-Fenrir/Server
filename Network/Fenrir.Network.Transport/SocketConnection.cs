using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Compression;
using Fenrir.Network.Transport.Logging;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

// Wraps an accepted Socket as an IDuplexPipe: receive-loop applies the legacy stream cipher (§3.4) as bytes
// land; send-loop drains TX back to the wire. Framing/decoding lives a layer above (Fenrir.Network.Framing).
public sealed class SocketConnection : IDuplexPipe, IAsyncDisposable
{
    private const int ReceiveBufferSize = 4096;

    // OS-level per-connection socket send/receive buffers (SO_SNDBUF/SO_RCVBUF), applied only when the
    // constructor's applyOsSocketBuffers flag is set. A completely separate knob from ReceiveBufferSize above
    // (a request-size hint to the Pipe's own buffer segment, not a socket option) and from PipeOptionsFactory's
    // in-process managed-memory watermarks -- these two constants govern how much the kernel buffers on the wire
    // before applying TCP flow control. Legacy applied them ONLY to zone (GameServer) accepted sockets
    // (Server/ts25zone/S02_MyServer.cpp:481 passes the "set buffers" flag on; the login/playuser/extra/center
    // accept paths and every listen socket leave it off via the default at Server/Header/socket.h:16,:95), so
    // this defaults off and only the GameServer opts in. Values are legacy's own (Server/Header/socket.h:33,:38):
    // SO_SNDBUF is deliberately ~10x SO_RCVBUF.
    private const int SocketSendBufferSize = 204800;
    private const int SocketReceiveBufferSize = 20480;

    // Governs ONLY the two loops' own blocking socket calls (_socket.ReceiveAsync/SendAsync) -- see Abort's
    // own remarks for why this exists separately from the pipe-level cancellation ClientSession.Abort already
    // performs on Transport.Input/Output.
    private readonly CancellationTokenSource _abortCts = new();
    private readonly ILogger? _logger;
    private readonly Pipe _rxPipe;

    private readonly Socket _socket;
    private readonly Pipe _txPipe;

    // applyOsSocketBuffers defaults off to match every non-zone accept path and every listen socket (legacy's
    // own default, Server/Header/socket.h:16,:95); only the GameServer's per-connection sockets pass it on
    // (Server/ts25zone/S02_MyServer.cpp:481). See SocketSendBufferSize/SocketReceiveBufferSize's own remarks.
    public SocketConnection(Socket socket, ILogger? logger = null, bool applyOsSocketBuffers = false)
    {
        _socket = socket;
        _socket.NoDelay = true; // Nagle OFF: an MMO's tick-driven traffic wants latency over throughput
        _logger = logger;

        // Applied after NoDelay, matching legacy's own option order (non-blocking -> TCP_NODELAY -> SO_SNDBUF ->
        // SO_RCVBUF, Server/Header/socket.h:18-41). A failure here (SocketException from either property setter)
        // propagates out of this constructor -- FenrirTcpListener's own new-SocketConnection try/catch then
        // disposes the socket and logs ConnectionConstructionFailed, which is exactly legacy's "a setsockopt
        // failure aborts acceptance of that socket" behavior (Server/Header/socket.h:32-41); it is never
        // swallowed into a half-configured connection.
        if (applyOsSocketBuffers)
        {
            _socket.SendBufferSize = SocketSendBufferSize;
            _socket.ReceiveBufferSize = SocketReceiveBufferSize;
        }

        RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;

        _rxPipe = new Pipe(PipeOptionsFactory.Rx);
        _txPipe = new Pipe(PipeOptionsFactory.Tx);
    }

    // Captured once at construction; used by the Application layer for IP-keyed anti-bruteforce (§8.5)
    // that a per-session token bucket can't cover.
    public IPEndPoint? RemoteEndPoint { get; }

    // One key snapshot per receive chunk (see ReceiveLoopAsync); relies on the client never sending re-keyed
    // bytes before the round trip that changes the key completes — would need to become per-frame otherwise.
    public Func<byte> GetInboundXorKey { get; set; } = static () => 0;

    // Closing unblocks in-flight ReceiveAsync/SendAsync; safe even if RunIOAsync never started (pipe completion is idempotent).
    public async ValueTask DisposeAsync()
    {
        _socket.Dispose();
        _abortCts.Dispose();

        await _rxPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _rxPipe.Writer.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Writer.CompleteAsync().ConfigureAwait(false);
    }

    public PipeReader Input => _rxPipe.Reader;
    public PipeWriter Output => _txPipe.Writer;

    // Never throws: both loops swallow their own faults and complete their pipe ends instead.
    public async Task RunIoAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abortCts.Token);
        await Task.WhenAll(ReceiveLoopAsync(linked.Token), SendLoopAsync(linked.Token)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Unblocks this connection's own in-flight/next <c>Socket.ReceiveAsync</c>/<c>SendAsync</c> call inside
    ///     <see cref="RunIoAsync" /> -- <em>not</em> the same thing as <c>ClientSession.Abort</c>, which only
    ///     cancels the RX/TX <see cref="Pipe" /> ends (<see cref="Input" />/<see cref="Output" />) and has no
    ///     effect on a socket-level read/write already blocked waiting on the wire. A session whose peer simply
    ///     stops sending bytes (the exact case an idle/liveness sweep exists to catch) would otherwise leave
    ///     <see cref="ReceiveLoopAsync" /> parked on <c>Socket.ReceiveAsync</c> indefinitely even after
    ///     <c>ClientSession.Abort</c> ran, since nothing else ever arrives to make that call return -- and every
    ///     resource a connection-host's teardown path frees only after <see cref="RunIoAsync" /> itself completes
    ///     (session-registry/flood-guard slots, the eventual <see cref="DisposeAsync" /> call) would stay held.
    ///     Safe to call multiple times or after <see cref="DisposeAsync" /> already ran (idempotent no-op via
    ///     <see cref="ObjectDisposedException" />, swallowed here -- disposal already tore down every loop this
    ///     would otherwise unblock). Does NOT itself dispose the socket -- same "cancel only, never own teardown"
    ///     posture as <c>ClientSession.Abort</c>; the connection host's own call site is still the one place that
    ///     calls <see cref="DisposeAsync" />.
    /// </summary>
    public void Abort()
    {
        try
        {
            _abortCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // DisposeAsync already ran (and already disposed _abortCts) -- both loops are long gone, nothing to unblock.
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var writer = _rxPipe.Writer;
        Exception? failure = null;

        try
        {
            while (true)
            {
                var memory = writer.GetMemory(ReceiveBufferSize);
                var bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                    break; // graceful FIN from the peer

                WireXor.ApplyStreamXor(memory.Span[..bytesRead], GetInboundXorKey());
                writer.Advance(bytesRead);

                var flushResult = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCompleted || flushResult.IsCanceled)
                    break; // reader side gone, or its ClientSession.Abort() cancelled this flush
            }
        }
        catch (Exception ex)
        {
            // Record failure so the paired reader (FrameDecoder/SessionLoop) observes why on its next read, instead of hanging.
            failure = ex;
        }
        finally
        {
            await writer.CompleteAsync(failure).ConfigureAwait(false);
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _txPipe.Reader;
        Exception? failure = null;

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    // SendAsync may send fewer bytes than requested under backpressure; loop until the segment is out.
                    var remaining = segment;
                    while (!remaining.IsEmpty)
                    {
                        var sent = await _socket.SendAsync(remaining, SocketFlags.None, cancellationToken)
                            .ConfigureAwait(false);
                        remaining = remaining[sent..];
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted || result.IsCanceled)
                    break;
            }
        }
        catch (Exception ex)
        {
            failure = ex;

            // Deliberately excludes OperationCanceledException: SocketConnection.Abort() cancels this same
            // linked token on EVERY connection teardown (see RunIoAsync/Abort's own remarks), including a
            // perfectly ordinary clean disconnect -- logging that as a fault would produce a Warning on every
            // single session end, not just the genuinely faulted ones. Anything else reaching here (a
            // SocketException from a mid-send connection reset, etc.) is a real anomaly worth surfacing --
            // see TransportLog.SendLoopFaulted's own remarks for why this is the one place this specific
            // fault can be observed at all (ReceiveLoopAsync's own fault resurfaces one layer up through
            // SessionLoop; this one, uniquely, does not).
            if (ex is not OperationCanceledException)
                _logger?.SendLoopFaulted(ex, RemoteEndPoint);
        }
        finally
        {
            await reader.CompleteAsync(failure).ConfigureAwait(false);
        }
    }
}
