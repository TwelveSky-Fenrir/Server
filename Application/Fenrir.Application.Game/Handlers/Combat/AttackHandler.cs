using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Combat;

/// <summary>
///     CZ_PROCESS_ATTACK_SEND (opcode 18). Only validates <c>mCase</c> is 1-6 (anti-fuzzing); guards, RNG
///     rolls and HP mutation all happen later on the zone's own tick thread.
/// </summary>
public sealed class AttackHandler : IInlinePacketHandler<AttackRequest>
{
    public void Handle(in AttackRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (packet.AttackInfo.Case is < 1 or > 6)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = zoneSession.CharacterId!.Value,
            AttackInfo = packet.AttackInfo
        });
    }
}
