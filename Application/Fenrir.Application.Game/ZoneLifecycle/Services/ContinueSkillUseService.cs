using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>Business logic for CZ_CONTINUE_SKILL_USE_SEND (op95) -- see <c>ContinueSkillUseHandler</c>'s remarks.</summary>
public interface IContinueSkillUseService
{
    AutoBuffActivationResolver.Result Activate(Zone zone, int characterId, PlayerRuntimeState state, int sort);
}

public sealed class ContinueSkillUseService : IContinueSkillUseService
{
    public AutoBuffActivationResolver.Result Activate(Zone zone, int characterId, PlayerRuntimeState state, int sort)
    {
        var context = new AutoBuffActivationResolver.Context(state.AutoBuffTime, state.ActionSort, state.Mana);
        var result = AutoBuffActivationResolver.Resolve(sort, in context, GameDate.Today());

        if (result.Kind == AutoBuffActivationResolver.ResultKind.Activate)
            zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId,
                ManaAfterActivation: result.ManaAfterActivation,
                ActionSort: AutoBuffActivationResolver.ChannelingActionSort, Broadcast: true));

        return result;
    }
}
