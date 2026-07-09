using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_CONTINUE_SKILL_STAT_SEND (op94) -- registers up to 8 auto-buff (skillId, grade) slots, clamping each
///     requested grade to the character's own currently-learned grade for that skill (see
///     <see cref="AutoBuffSkillResolver" />'s remarks). Always replies Result=0, even when every slot clamps to
///     an unlearned skill's -1.
/// </summary>
public sealed class ContinueSkillStatHandler(
    IContinueSkillStatService service,
    ILogger<ContinueSkillStatHandler> logger)
    : IInlinePacketHandler<ContinueSkillStatRequest>
{
    public void Handle(in ContinueSkillStatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: ContinueSkillStatRequest (op94) received for character {CharacterId}",
            session.SessionId, characterId);

        service.RegisterAutoBuffs(zone, characterId, state, packet.Skill);
        logger.LogInformation("Character {CharacterId} registered auto-buff skill slots", characterId);
        session.Send(new AutoBuffRegisterResponse { Value = 0 });
    }
}
