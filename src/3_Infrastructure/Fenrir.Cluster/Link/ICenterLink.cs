using Fenrir.Core.Abstractions;

namespace Fenrir.Cluster.Link;

/// <summary>
///     The consumer-facing API of the outbound CenterServer link. A single process-wide instance is provided by
///     <c>CenterLinkClientHost</c>; Login/Game code pushes S2S events through <see cref="Send{TPacket}" /> without
///     ever caring whether the link is currently up.
/// </summary>
public interface ICenterLink
{
    /// <summary>
    ///     True once an authenticated uplink to the CenterServer is live. Flips back to false during a
    ///     reconnect/backoff window.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    ///     Fire-and-forget send of one outbound S2S frame (1-byte opcode header, no compression, no XOR — the same
    ///     write path <c>ClientSession.Send</c> uses). Best-effort: silently no-ops when the uplink is down and
    ///     never blocks or throws into the caller (safe to call from a zone tick / handler thread). Backpressure is
    ///     handled by the underlying <c>ClientSession</c> send path.
    /// </summary>
    void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
