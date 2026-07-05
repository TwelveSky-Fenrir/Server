using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ContinueSkillStatService : IContinueSkillStatService
{
    public void RegisterAutoBuffs(Zone zone, int characterId, PlayerRuntimeState state, int[] skill)
    {
        var registered = AutoBuffSkillResolver.ResolveRegistration(skill, state.LearnedSkills);
        zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId, registered));
    }
}
