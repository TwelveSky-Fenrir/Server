using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>Business logic for CZ_CONTINUE_SKILL_STAT_SEND (op94) -- see <c>ContinueSkillStatHandler</c>'s remarks.</summary>
public interface IContinueSkillStatService
{
    void RegisterAutoBuffs(Zone zone, int characterId, PlayerRuntimeState state, int[] skill);
}

public sealed class ContinueSkillStatService : IContinueSkillStatService
{
    public void RegisterAutoBuffs(Zone zone, int characterId, PlayerRuntimeState state, int[] skill)
    {
        var registered = AutoBuffSkillResolver.ResolveRegistration(skill, state.LearnedSkills);
        zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId, registered));
    }
}
