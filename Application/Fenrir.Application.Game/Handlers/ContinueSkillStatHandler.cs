using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_CONTINUE_SKILL_STAT_SEND (op94) -- registers up to 8 auto-buff (skillId, grade) slots, clamping each
///     requested grade to the character's own currently-learned grade for that skill (see
///     <see cref="AutoBuffSkillResolver" />'s remarks). Always replies Result=0, even when every slot clamps to
///     an unlearned skill's -1.
/// </summary>
public sealed class ContinueSkillStatHandler : IInlinePacketHandler<ContinueSkillStatRequest>
{
    public void Handle(in ContinueSkillStatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var registered = AutoBuffSkillResolver.ResolveRegistration(packet.Skill, state.LearnedSkills);

        session.Send(new AutoBuffRegisterResponse { Value = 0 });
        zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId, registered));
    }
}
