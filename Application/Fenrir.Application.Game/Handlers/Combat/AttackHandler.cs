using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Combat;

/// <summary>
///     CZ_PROCESS_ATTACK_SEND (opcode 18, report 05 §4). Zero SQL, zero validation here -- this handler only
///     enforces the ONE thing the legacy checks before even entering <c>MyGame::ProcessAttackXX</c>: <c>mCase</c>
///     must be 1-6, anything else is anti-fuzzing <c>Quit()</c> (contract doc on <see cref="AttackRequest" />).
///     Everything else (guards, RNG rolls, HP mutation) happens on the zone's own tick thread --
///     see <see cref="CombatCommand" />'s remarks for why this is a raw forward, not a pre-decided result,
///     unlike the Inventory pass's async handlers.
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

        // InWorld gating guarantees CurrentZone was set during registration; the pattern match is the cheap
        // belt-and-braces for the (benign, documented) staleness window around a handoff -- same posture as
        // every other InWorld handler (AvatarActionHandler, ZoneMoveHandler).
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = zoneSession.CharacterId!.Value,
            AttackInfo = packet.AttackInfo
        });
    }
}
