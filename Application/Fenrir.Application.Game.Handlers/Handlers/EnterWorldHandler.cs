using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op12, world-entry handler. ZC_REGISTER_AVATAR_RECV carries no Result field, so any anti-tamper failure
///     here closes the socket rather than replying with a clean failure. Business logic lives in
///     <see cref="IEnterWorldService" />.
/// </summary>
/// <remarks>
///     Fenrir-only observability addition, no legacy citation: logs one Information-level "request received"
///     line before delegating to <see cref="IEnterWorldService" /> -- deliberately ahead of every
///     precondition/gate check that service runs (firewall, ban, tID mismatch, action-type, tribe
///     consistency, map hosting). <c>SessionLoop</c>'s own per-frame "frame received" diagnostic
///     (Network/Fenrir.Network.Dispatch/SessionLoop.cs) is Debug-level for every opcode with no op12-specific
///     branch, so absence of that line from an Info-level-or-above log capture is not evidence op12 was
///     never sent -- it is equally consistent with op12 having arrived and something downstream failing
///     silently. This line closes that disambiguation gap for op12/EnterWorldRequest specifically without
///     changing SessionLoop's own log level for every other opcode.
/// </remarks>
public sealed class EnterWorldHandler(IEnterWorldService service, ILogger<EnterWorldHandler>? logger = null)
    : IAsyncPacketHandler<EnterWorldRequest>
{
    public ValueTask HandleAsync(EnterWorldRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogInformation(
            "Session {SessionId}: EnterWorldRequest (op12) received for account {AccountId} character {CharacterId}",
            session.SessionId, zoneSession.AccountId, zoneSession.CharacterId);

        return service.HandleAsync(packet, zoneSession, cancellationToken);
    }
}
