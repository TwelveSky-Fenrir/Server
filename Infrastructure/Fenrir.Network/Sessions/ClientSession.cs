using System.IO.Pipelines;
using System.Net;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Sessions;

// Owns the duplex pipe transport and the send-side lock; state-machine specifics live in the subclasses.
public abstract class ClientSession : IPacketSession
{
    private readonly Lock _sendLock = new();
    private int _completed;

    protected ClientSession(long sessionId, IDuplexPipe transport, FenrirServer server,
        IPEndPoint? remoteEndPoint = null)
    {
        SessionId = sessionId;
        Transport = transport;
        Server = server;
        RemoteEndPoint = remoteEndPoint;
    }

    public IDuplexPipe Transport { get; }

    public FenrirServer Server { get; }

    /// <summary>The peer's address; null for a transport never backed by a real accepted socket.</summary>
    public IPEndPoint? RemoteEndPoint { get; }

    /// <summary>Legacy <c>mPacketEncryptionValue</c> (§3.4): 0 until the greeting packet seeds it.</summary>
    public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason { get; private set; }

    /// <summary>Process-local, monotonically increasing — never persisted, never sent to the client.</summary>
    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();

        lock (_sendLock)
        {
            var span = Transport.Output.GetSpan(total);
            FrameWriter.WriteFrame(in packet, span);
            Transport.Output.Advance(total);
        }

        FlushOutput();
    }

    /// <summary>Checked by <see cref="Dispatching.SessionLoop" /> before dispatch against the generated <c>SessionStateGate</c>.</summary>
    public abstract bool IsOpcodeAllowed(byte opcode);

    // Sends a fully pre-built frame as-is — the LZ4/ZPACKET path for Compressed M1 packets, whose bytes
    // already come out of the generated MessageFactory.Encode.
    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        lock (_sendLock)
        {
            var span = Transport.Output.GetSpan(rawFrame.Length);
            rawFrame.CopyTo(span);
            Transport.Output.Advance(rawFrame.Length);
        }

        FlushOutput();
    }

    private void FlushOutput()
    {
        var flush = Transport.Output.FlushAsync();
        if (!flush.IsCompletedSuccessfully)
            _ = ObserveFlushAsync(flush);
    }

    private static async ValueTask ObserveFlushAsync(ValueTask<FlushResult> flush)
    {
        try
        {
            await flush.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Receive loop will independently notice the broken pipe; a failed flush here must not fault the caller's Send<T>.
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
