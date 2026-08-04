using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ContinueSkillUseService : IContinueSkillUseService
{
    public async ValueTask<AutoBuffActivationResolver.Result> ActivateAsync(Zone zone, int characterId,
        PlayerRuntimeState state, int sort, CancellationToken cancellationToken)
    {
        if (!state.CanIssueGameplayActions)
            return new AutoBuffActivationResolver.Result(AutoBuffActivationResolver.ResultKind.Rejected);

        var context = new AutoBuffActivationResolver.Context(state.AutoBuffTime, state.ActionSort, state.Mana);
        var result = AutoBuffActivationResolver.Resolve(sort, in context, GameDate.Today());

        switch (result.Kind)
        {
            case AutoBuffActivationResolver.ResultKind.Activate:
                return (await zone.PostAutoBuffCommandAndWaitForResultAsync(
                        new AutoBuffZoneCommand(characterId,
                            ActivateAutoBuff: true,
                            ActionSort: AutoBuffActivationResolver.ChannelingActionSort,
                            Broadcast: true), cancellationToken).ConfigureAwait(false)).Kind == ZoneCommandResultKind.Applied
                    ? result
                    : new AutoBuffActivationResolver.Result(AutoBuffActivationResolver.ResultKind.Rejected);

            case AutoBuffActivationResolver.ResultKind.Tick:
                return (await zone.PostAutoBuffCommandAndWaitForResultAsync(
                        new AutoBuffZoneCommand(characterId, ApplyRegisteredBuffs: true), cancellationToken)
                        .ConfigureAwait(false)).Kind == ZoneCommandResultKind.Applied
                    ? result
                    : new AutoBuffActivationResolver.Result(AutoBuffActivationResolver.ResultKind.Rejected);
        }

        return result;
    }
}
