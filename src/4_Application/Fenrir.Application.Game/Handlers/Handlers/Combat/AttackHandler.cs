using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Combat;

public sealed class AttackHandler(IAttackService service, ILogger<AttackHandler>? logger = null)
    : IInlinePacketHandler<AttackRequest>
{
    public void Handle(in AttackRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var attackInfo = packet.AttackInfo;

        logger?.AttackReceived(session.SessionId, characterId, attackInfo.Case, attackInfo.ServerIndex2);

        if (!service.IsValidCase(attackInfo.Case))
        {
            logger?.LogInformation(
                "Attack rejected and character {CharacterId}'s session will be terminated: case {CaseValue} is outside the valid 1-6 range (anti-fuzzing)",
                characterId, attackInfo.Case);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        service.PostAttack(zone, characterId, in attackInfo);
    }
}
