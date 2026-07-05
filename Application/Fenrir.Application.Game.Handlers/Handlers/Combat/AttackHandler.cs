using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Combat;

/// <summary>
///     CZ_PROCESS_ATTACK_SEND (opcode 18). Only validates <c>mCase</c> is 1-6 (anti-fuzzing); guards, RNG
///     rolls and HP mutation all happen later on the zone's own tick thread.
/// </summary>
public sealed class AttackHandler(IAttackService service) : IInlinePacketHandler<AttackRequest>
{
    public void Handle(in AttackRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (!service.IsValidCase(packet.AttackInfo.Case))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var attackInfo = packet.AttackInfo;
        service.PostAttack(zone, zoneSession.CharacterId!.Value, in attackInfo);
    }
}
