using Fenrir.Network.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Logging;

/// <summary>
///     Packet-level operational log entries for the two network dispatch chokepoints every frame passes
///     through -- <see cref="Fenrir.Network.Dispatch.SessionLoop" />'s decoded-frame branch (received) and
///     <see cref="Fenrir.Network.Dispatch.Sessions.ClientSession.Send{TPacket}" />/
///     <see cref="Fenrir.Network.Dispatch.Sessions.ClientSession.SendRaw" /> (sent). This is operational/
///     diagnostic logging surfaced through standard <c>Microsoft.Extensions.Logging</c> -> OpenTelemetry ->
///     the Aspire dashboard, deliberately distinct from <c>game.EventLog</c> (the durable SQL gameplay audit
///     trail) -- nothing here is meant to be queried after the fact, only observed live while the process runs.
///     <para>
///         Every method is <see cref="LogLevel.Debug" />, generated via <c>[LoggerMessage]</c> so each one gets
///         the compiler-emitted <c>IsEnabled</c> check and non-boxing typed state -- see the Microsoft Learn
///         guidance on the LoggerMessage source generator. Both servers' <c>appsettings*.json</c> set
///         <c>Logging:LogLevel:Default</c> to <c>Information</c>, so at a normal production log level every
///         call below costs exactly one virtual <c>IsEnabled</c> check and nothing else -- no boxing, no
///         string formatting, no allocation. A hand-written <c>logger.LogDebug("...", args)</c> call instead
///         would box every value-type argument into the <c>params object?[]</c> array *before* that check
///         ever runs, on every single received/sent packet, which is exactly the per-packet allocation this
///         class exists to avoid (see the <c>dotnet-game-server-performance</c> skill's packet-dispatch
///         section). Do not add a new hand-written LogDebug/LogTrace call for a packet-level event; add a
///         method here instead so every packet-level log entry shares this same near-zero-cost-when-disabled
///         shape.
///     </para>
///     <para>
///         Deliberately does not log raw payload bytes. Two independent reasons: (1) perf -- formatting/
///         copying a payload (hex or otherwise) is real per-packet work that a mere <c>IsEnabled</c> check
///         cannot skip once the call is made, and packet payloads can be large (e.g. WorldSnapshotResponse);
///         (2) data sensitivity -- payloads carry <c>ObfuscatedUidField</c>-obfuscated account/character
///         identifiers, chat text, item/trade data, none of which belongs in an operational log sink that
///         (via the Aspire dashboard/OTLP exporter) is reachable by anyone with operational access to the
///         cluster, a materially different audience than the DB-backed <c>game.EventLog</c> audit trail. If a
///         genuine need for payload-level tracing ever arises (e.g. diagnosing a wire-desync bug live), it
///         should be added as its own explicit, separately-gated <see cref="LogLevel.Trace" /> method here --
///         one level more verbose than everything else in this class, never folded into
///         <see cref="PacketReceived" />/<see cref="PacketSent" /> -- and called from its own opt-in
///         diagnostic path, not unconditionally from <c>SessionLoop</c>/<c>ClientSession</c>. No such need
///         exists today, so it is not implemented.
///     </para>
/// </summary>
internal static partial class PacketLog
{
    /// <summary>
    ///     Every successfully decoded incoming frame, logged once from <c>SessionLoop.ProcessBufferAsync</c>
    ///     immediately after <c>FrameDecoder.TryReadFrame</c> succeeds (before the state-gate/rate-limit/
    ///     dispatch steps that can still abort the session). <paramref name="decodeMicroseconds" /> times only
    ///     the decode call itself (a 9-byte header copy + an <c>OpcodeRegistry</c> size lookup, no payload
    ///     copy) -- callers must gate the <see cref="System.Diagnostics.Stopwatch" /> capture behind an
    ///     explicit <c>logger.IsEnabled(LogLevel.Debug)</c> check of their own (see
    ///     <c>SessionLoop.ProcessBufferAsync</c>) rather than relying on this method's own generated check,
    ///     since the whole point is to avoid paying for the timestamp capture itself when Debug is disabled --
    ///     this method's own internal check alone cannot skip work its caller already did before invoking it.
    /// </summary>
    [LoggerMessage(
        EventId = 4001,
        EventName = "PacketReceived",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: packet received, server {Server} opcode {Opcode} ({ByteSize} bytes, decoded in {DecodeMicroseconds:F1} us)")]
    public static partial void PacketReceived(
        this ILogger logger,
        long sessionId,
        FenrirServer server,
        byte opcode,
        int byteSize,
        double decodeMicroseconds);

    /// <summary>
    ///     Every frame handed to the transport by <c>ClientSession.Send{TPacket}</c>/<c>SendRaw</c> -- both the
    ///     fast uncontended-lock path and the queued-under-backpressure path call this exactly once per Send/
    ///     SendRaw invocation, right after the frame bytes are written/enqueued (not gated on the eventual
    ///     flush completing, since from the session's own perspective the packet is already "sent" once handed
    ///     off). No extra work happens to compute <paramref name="opcode" />/<paramref name="byteSize" />
    ///     beyond values the caller already has in hand, so callers rely on this method's own generated
    ///     <c>IsEnabled</c> check rather than wrapping it in their own -- unlike <see cref="PacketReceived" />,
    ///     which additionally gates a <see cref="System.Diagnostics.Stopwatch" /> capture.
    /// </summary>
    [LoggerMessage(
        EventId = 4002,
        EventName = "PacketSent",
        Level = LogLevel.Debug,
        Message = "Session {SessionId}: packet sent, opcode {Opcode} ({ByteSize} bytes)")]
    public static partial void PacketSent(this ILogger logger, long sessionId, byte opcode, int byteSize);

    /// <summary>
    ///     Every decoded frame whose dispatch (<c>IFrameDispatcher.DispatchAsync</c> -- the generated
    ///     <c>MessageDispatcher</c>'s handler resolution and invocation together) completed without throwing,
    ///     logged once from <c>SessionLoop.ProcessBufferAsync</c> immediately after the
    ///     <c>await dispatcher.DispatchAsync(...)</c> call returns, right before <c>ClientSession.Touch()</c>
    ///     stamps liveness. <paramref name="dispatchMicroseconds" /> times handler resolution AND execution
    ///     together -- unlike <see cref="PacketReceived" />'s <c>decodeMicroseconds</c>, which times only the
    ///     frame decode itself. There is deliberately no separate "handler resolved, not yet invoked" log
    ///     point: the generated <c>MessageDispatcher</c> switch (<c>Fenrir.Generators.Dispatch.
    ///     HandlerDispatchIncrementalGenerator</c>) resolves a handler and invokes it in the same generated
    ///     statement with no externally observable seam between the two, so this method's placement -- right
    ///     after dispatch completes -- is the finest dispatch-timing granularity obtainable from
    ///     <c>Fenrir.Network.Dispatch</c> without a generator change (out of this project's territory; see
    ///     that generator's own file for who owns it). Never fires for a frame rejected before reaching
    ///     dispatch (state-gate/rate-limit -- see <c>SessionLoop</c>'s own <c>LogWarning</c> calls for those)
    ///     or whose handler threw (see <c>SessionLoop</c>'s own <c>LogError</c> call for that) -- so, read
    ///     together with those, this entry's mere presence already confirms "resolved a handler and it ran to
    ///     completion" for that opcode. Same caller-gated-<see cref="System.Diagnostics.Stopwatch" />-capture
    ///     contract as <see cref="PacketReceived" />.
    /// </summary>
    [LoggerMessage(
        EventId = 4003,
        EventName = "PacketDispatched",
        Level = LogLevel.Debug,
        Message =
            "Session {SessionId}: packet dispatched, server {Server} opcode {Opcode} (handled in {DispatchMicroseconds:F1} us)")]
    public static partial void PacketDispatched(
        this ILogger logger,
        long sessionId,
        FenrirServer server,
        byte opcode,
        double dispatchMicroseconds);
}
