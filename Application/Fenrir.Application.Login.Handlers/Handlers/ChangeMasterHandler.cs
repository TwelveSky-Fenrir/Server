using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op24 CL_CHANGE_MASTER_SEND — legacy handler body is empty (S04_MyWork02.cpp l.1655-1658), teacher/master
///     feature removed without cleanup; consumed silently, never replies.
/// </summary>
/// <remarks>
///     Legacy's own connection-liveness loop (Server/ts25login/S07_MyGame01.cpp:37-73) happens not to refresh
///     its idle-timer marker from op24 traffic specifically -- unique among that source file's handlers.
///     Fenrir's <see cref="Fenrir.Network.Dispatch.SessionLoop" /> deliberately does NOT reproduce that one-
///     opcode asymmetry (every successfully-dispatched frame, op24 included, refreshes
///     <c>ClientSession.LastActivityUtc</c>) -- see <c>SessionLoop.ProcessBufferAsync</c>'s own comment at its
///     <c>session.Touch()</c> call site for the full rationale.
/// </remarks>
public sealed class ChangeMasterHandler(ILogger<ChangeMasterHandler> logger) : IInlinePacketHandler<ChangeMasterRequest>
{
    public void Handle(in ChangeMasterRequest packet, IPacketSession session)
    {
        // Per-packet Debug entry, gated like every other handler's dispatch-entry log (see SessionLoop's own
        // debugEnabled local) -- op24 is a live no-op with no business logic to observe, so this is the only
        // trace this handler will ever produce, but a silently-consumed opcode is otherwise indistinguishable
        // from one that was never dispatched at all.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op24 CL_CHANGE_MASTER_SEND received and ignored (legacy no-op feature)",
                session.SessionId);
    }
}
