using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ContinueSkillUseService : IContinueSkillUseService
{
    public AutoBuffActivationResolver.Result Activate(Zone zone, int characterId, PlayerRuntimeState state, int sort)
    {
        var context = new AutoBuffActivationResolver.Context(state.AutoBuffTime, state.ActionSort, state.Mana);
        var result = AutoBuffActivationResolver.Resolve(sort, in context, GameDate.Today());

        if (result.Kind == AutoBuffActivationResolver.ResultKind.Activate)
            zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId,
                ActivateAutoBuff: true,
                ActionSort: AutoBuffActivationResolver.ChannelingActionSort, Broadcast: true));

        return result;
    }
}
