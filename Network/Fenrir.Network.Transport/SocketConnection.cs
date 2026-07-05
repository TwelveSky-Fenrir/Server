using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Compression;

namespace Fenrir.Network.Transport;

// Wraps an accepted Socket as an IDuplexPipe: receive-loop applies the legacy stream cipher (§3.4) as bytes
// land; send-loop drains TX back to the wire. Framing/decoding lives a layer above (Fenrir.Network.Framing).
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

        await _rxPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _rxPipe.Writer.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Writer.CompleteAsync().ConfigureAwait(false);
    }

    public PipeReader Input => _rxPipe.Reader;
    public PipeWriter Output => _txPipe.Writer;

    // Never throws: both loops swallow their own faults and complete their pipe ends instead.
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
        }
        finally
        {
            await reader.CompleteAsync(failure).ConfigureAwait(false);
        }
    }
}
