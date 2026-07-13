using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Link;

/// <summary>
///     Client-side <see cref="ClientSession" /> for the outbound CenterServer link — the mirror of the accept-side
///     <c>CenterLinkSession</c>. Reuses <see cref="ClientSession" /> purely for its battle-tested, backpressure-aware
///     1-byte-header send path (<c>Send</c>/<c>SendRaw</c>) and because <c>IFrameDispatcher.DispatchAsync</c> needs
///     an <c>IPacketSession</c> to route received fan-out frames through. No client XOR: the S2S stream is in clear
///     (the inbound XOR key is left at its default 0 => <c>WireXor.ApplyStreamXor</c> is a no-op).
/// </summary>
internal sealed class CenterUplinkSession(
    long sessionId,
    SocketConnection connection,
    ILogger? logger = null)
    : ClientSession(sessionId, connection, FenrirServer.Center, connection.RemoteEndPoint, logger)
{
    /// <summary>
    ///     Inbound opcodes arrive only from the CenterServer, which is trusted once the HMAC handshake completes, so
    ///     there is no client-side state gate to enforce (the frame reader's opcode-size provider already rejects
    ///     anything unrecognized). Allow-all is intentional.
    /// </summary>
    public override bool IsOpcodeAllowed(byte opcode)
    {
        return true;
    }
}
