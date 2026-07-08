using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Combat;

/// <summary>
///     CZ_PROCESS_ATTACK_SEND (opcode 18). Only validates <c>mCase</c> is 1-6 (anti-fuzzing); guards, RNG
///     rolls and HP mutation all happen later on the zone's own tick thread.
/// </summary>
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
            // Anti-fuzzing rejection (AttackRequest's own remarks) -- Information, not Debug: an operator
            // watching live logs should see a session-terminating anti-cheat gate without raising the
            // default log level, even though it isn't a "successful" outcome.
            logger?.LogInformation(
                "Attack rejected and character {CharacterId}'s session will be terminated: case {CaseValue} is outside the valid 1-6 range (anti-fuzzing)",
                characterId, attackInfo.Case);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        service.PostAttack(zone, characterId, in attackInfo);
    }
}
