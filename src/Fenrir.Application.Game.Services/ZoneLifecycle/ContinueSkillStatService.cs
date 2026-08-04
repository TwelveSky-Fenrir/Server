using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ContinueSkillStatService(WorldDataCache worldData) : IContinueSkillStatService
{
    public async ValueTask<bool> RegisterAutoBuffsAsync(Zone zone, int characterId, PlayerRuntimeState state,
        int[] skill, CancellationToken cancellationToken)
    {
        var registered = AutoBuffSkillResolver.ResolveRegistration(skill, state.LearnedSkills,
            worldData.SkillsById.ContainsKey);
        return (await zone.PostAutoBuffCommandAndWaitForResultAsync(
                   new AutoBuffZoneCommand(characterId, registered), cancellationToken).ConfigureAwait(false)).Kind ==
               ZoneCommandResultKind.Applied;
    }
}
