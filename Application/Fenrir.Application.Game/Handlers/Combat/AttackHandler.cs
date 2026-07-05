using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Combat;

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
