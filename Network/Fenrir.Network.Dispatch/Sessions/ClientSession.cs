using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Sessions;

// Owns the duplex pipe transport and the send-side lock; state-machine specifics live in the subclasses.
public abstract class ClientSession(
    long sessionId,
    IDuplexPipe transport,
    FenrirServer server,
    IPEndPoint? remoteEndPoint = null)
    : IPacketSession
{
    private const int SlowConsumerBackpressureStreakLimit = 5;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _backpressureStreak;
    private int _completed;

    public IDuplexPipe Transport { get; } = transport;

    public FenrirServer Server { get; } = server;

    /// <summary>The peer's address; null for a transport never backed by a real accepted socket.</summary>
    public IPEndPoint? RemoteEndPoint { get; } = remoteEndPoint;

    /// <summary>Legacy <c>mPacketEncryptionValue</c> (§3.4): 0 until the greeting packet seeds it.</summary>
    public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason { get; private set; }

    /// <summary>Process-local, monotonically increasing — never persisted, never sent to the client.</summary>
    public long SessionId { get; } = sessionId;

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();

        _sendLock.Wait();
        try
        {
            var span = Transport.Output.GetSpan(total);
            FrameWriter.WriteFrame(in packet, span);
            Transport.Output.Advance(total);
        }
        catch
        {
            _sendLock.Release();
            throw;
        }

        FlushLocked();
    }

    /// <summary>
    ///     Checked by <see cref="Dispatching.SessionLoop" /> before dispatch against the generated
    ///     <c>SessionStateGate</c>.
    /// </summary>
    public abstract bool IsOpcodeAllowed(byte opcode);

    // Sends a fully pre-built frame as-is — the LZ4/ZPACKET path for Compressed M1 packets, whose bytes
    // already come out of the generated MessageFactory.Encode.
    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        _sendLock.Wait();
        try
        {
            var span = Transport.Output.GetSpan(rawFrame.Length);
            rawFrame.CopyTo(span);
            Transport.Output.Advance(rawFrame.Length);
        }
        catch
        {
            _sendLock.Release();
            throw;
        }

        FlushLocked();
    }

    // Caller must already hold _sendLock; releases it once this call's flush resolves, so no second Send()
    // can start GetSpan/Advance/FlushAsync while this one is still outstanding (unsafe per PipeWriter's docs).
    private void FlushLocked()
    {
        var flush = Transport.Output.FlushAsync();

        if (flush.IsCompletedSuccessfully)
        {
            _backpressureStreak = 0;
            _sendLock.Release();
            return;
        }

        _ = ObserveFlushAsync(flush);
    }

    // A ValueTask<FlushResult> that doesn't complete synchronously means this send hit the pipe's
    // PauseWriterThreshold; FlushResult.IsCompleted/IsCanceled reflect reader completion/CancelPendingFlush,
    // not backpressure, so the synchronous-completion check above is the only backpressure signal available.
    private async ValueTask ObserveFlushAsync(ValueTask<FlushResult> flush)
    {
        try
        {
            var result = await flush.ConfigureAwait(false);
            if (result is { IsCanceled: false, IsCompleted: false } &&
                ++_backpressureStreak >= SlowConsumerBackpressureStreakLimit)
                Abort(Sessions.DisconnectReason.SlowConsumer);
        }
        catch (Exception)
        {
            // Receive loop will independently notice the broken pipe; a failed flush here must not fault the caller's Send<T>.
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Idempotent: only the first caller's <paramref name="reason" /> sticks.</summary>
    public void Abort(DisconnectReason reason)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        DisconnectReason = reason;
        Transport.Input.CancelPendingRead();
        Transport.Output.CancelPendingFlush();
    }

    public async ValueTask CompleteAsync()
    {
        await Transport.Input.CompleteAsync().ConfigureAwait(false);
        await Transport.Output.CompleteAsync().ConfigureAwait(false);
    }
}
