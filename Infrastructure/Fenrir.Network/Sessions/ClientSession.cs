using System.IO.Pipelines;
using System.Net;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Sessions;

/// <summary>
///     The one mutable thing on the network side of a connection. Owns the duplex pipe transport and the
///     send-side lock; state-machine specifics live in <see cref="LoginClientSession" />/<see cref="ZoneClientSession" />.
/// </summary>
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

    /// <summary>
    ///     The peer's address (see <see cref="Transport.SocketConnection.RemoteEndPoint" />); null for a transport never
    ///     backed by a real accepted socket.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; }

    /// <summary>
    ///     <c>mPacketEncryptionValue</c> (§3.4): 0 until the greeting packet seeds it, then applied to every inbound byte
    ///     by the receive loop.
    /// </summary>
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

    /// <summary>
    ///     Checked by <see cref="Dispatching.SessionLoop" /> before dispatch: is this opcode legal in the session's
    ///     current state (generated <c>SessionStateGate</c>)?
    /// </summary>
    public abstract bool IsOpcodeAllowed(byte opcode);

    /// <summary>
    ///     Sends a fully pre-built frame as-is (opcode byte included) — the LZ4/ZPACKET path for the two
    ///     <c>Compressed</c> M1 packets, whose bytes already come out of the generated <c>MessageFactory.Encode</c>.
    /// </summary>
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
            // The receive/session loop will independently notice the broken pipe and tear the session down;
            // a failed flush here must never fault an unrelated caller's synchronous Send<T>.
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
