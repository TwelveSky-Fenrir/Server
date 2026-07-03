using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Transport;

/// <summary>
///     Wraps one already-accepted <see cref="Socket" /> as an <see cref="IDuplexPipe" />: a receive-loop pumps raw
///     bytes off the wire into the RX pipe (applying the legacy stream cipher, §3.4, byte-for-byte as they land),
///     and a send-loop drains the TX pipe — whatever <see cref="Sessions.ClientSession.Send{TPacket}" />/
///     <c>SendRaw</c> write into <see cref="Output" /> — back out to the wire. Framing/decoding is a layer above
///     this (Fenrir.Network.Framing); this class only ever moves bytes.
/// </summary>
public sealed class SocketConnection : IDuplexPipe, IAsyncDisposable
{
    private const int ReceiveBufferSize = 4096;
    private readonly Pipe _rxPipe;

    private readonly Socket _socket;
    private readonly Pipe _txPipe;

    public SocketConnection(Socket socket)
    {
        _socket = socket;
        _socket.NoDelay = true; // Nagle OFF: an MMO's tick-driven traffic wants latency over throughput

        RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;

        _rxPipe = new Pipe(PipeOptionsFactory.Rx);
        _txPipe = new Pipe(PipeOptionsFactory.Tx);
    }

    /// <summary>
    ///     The peer's address, captured once at construction (an accepted socket's remote endpoint never changes).
    ///     Null only for a transport that was never a real accepted socket (e.g. a test double never hits this
    ///     constructor). Consumed by the Application layer for IP-keyed concerns (login anti-bruteforce, §8.5)
    ///     that a per-session token bucket cannot cover, since a fresh TCP connection always gets a fresh SessionId.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; }

    /// <summary>
    ///     Supplies the current <c>mPacketEncryptionValue</c> key for each inbound chunk. Deliberately a delegate
    ///     rather than a plain settable byte: the canonical key already lives on
    ///     <see cref="Sessions.ClientSession.InboundStreamXorKey" />, and the session can only be constructed from
    ///     this connection's <see cref="IDuplexPipe" /> view — i.e. after this <see cref="SocketConnection" />
    ///     already exists (see <see cref="FenrirTcpListener" />). Wiring <c>() =&gt; session.InboundStreamXorKey</c>
    ///     in once, right after the session is created, gives one single source of truth for the key with nothing
    ///     to keep in sync, instead of a second copy that every seed/reset would have to update in two places.
    ///     Defaults to "unseeded" (key 0 is a no-op per <see cref="LegacyXor.ApplyStreamXor" />).
    /// </summary>
    /// <remarks>
    ///     Read once per <see cref="Socket.ReceiveAsync(Memory{byte}, SocketFlags, CancellationToken)" /> chunk and
    ///     applied uniformly to that whole chunk (see <see cref="ReceiveLoopAsync" />) — this relies on an unenforced
    ///     timing invariant: the key can only change as a reaction to a fully-decoded frame further up the pipeline,
    ///     which itself can only happen after the bytes carrying that frame have already left this delegate. A
    ///     client can therefore never have bytes in flight that need the new key spliced into the same OS-level
    ///     read as bytes that still need the old one, *unless* it starts sending re-keyed bytes without waiting for
    ///     the round trip that tells it to. If that ever changes (pipelined greeting + follow-up in one send(), or
    ///     a future re-keying protocol), this per-chunk snapshot would need to become a per-byte/per-frame one.
    /// </remarks>
    public Func<byte> GetInboundXorKey { get; set; } = static () => 0;

    /// <summary>
    ///     Closes the socket (unblocking any in-flight ReceiveAsync/SendAsync so the loops above can observe the failure)
    ///     and completes both pipes. Safe to call even if <see cref="RunIOAsync" /> was never started, or already finished on
    ///     its own — pipe completion is idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _socket.Dispose();

        await _rxPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _rxPipe.Writer.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Writer.CompleteAsync().ConfigureAwait(false);
    }

    public PipeReader Input => _rxPipe.Reader;
    public PipeWriter Output => _txPipe.Writer;

    /// <summary>
    ///     Runs the receive- and send-loops side by side until both end (peer closed, error, or cancellation). Never
    ///     throws — both loops swallow their own faults and complete their pipe ends instead, so there is nothing left to
    ///     observe.
    /// </summary>
    public Task RunIOAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(ReceiveLoopAsync(cancellationToken), SendLoopAsync(cancellationToken));
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

                // One key snapshot for the whole chunk — see the timing-invariant remark on GetInboundXorKey.
                LegacyXor.ApplyStreamXor(memory.Span[..bytesRead], GetInboundXorKey());
                writer.Advance(bytesRead);

                var flushResult = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCompleted || flushResult.IsCanceled)
                    break; // reader side gone, or its ClientSession.Abort() cancelled this flush
            }
        }
        catch (Exception ex)
        {
            // Any socket/cancellation failure ends this loop the same way: record it so the paired reader
            // (FrameDecoder/SessionLoop, reading Input) observes *why* on its next read instead of hanging.
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
                    // Socket.SendAsync may send fewer bytes than requested under backpressure (the exact slow-client
                    // case the TX pipe thresholds exist to guard against) — loop until the whole segment is out.
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
        }
        finally
        {
            await reader.CompleteAsync(failure).ConfigureAwait(false);
        }
    }
}
