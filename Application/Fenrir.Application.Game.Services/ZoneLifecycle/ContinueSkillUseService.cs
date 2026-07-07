using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ContinueSkillUseService : IContinueSkillUseService
{
    /// <remarks>
    ///     The activate/no-reply/disconnect decision below still reads <c>state.AutoBuffTime</c>/<c>ActionSort</c>/
    ///     <c>Mana</c> as a snapshot on the session thread -- that gate is unchanged. What changed is that the
    ///     posted command no longer carries <c>result.ManaAfterActivation</c> (computed from that same stale
    ///     snapshot) as a literal to apply verbatim: <see cref="AutoBuffZoneCommand.ActivateAutoBuff" /> only
    ///     requests the debit, and <see cref="Zone.ApplyAutoBuffCommand" /> recomputes it from the tick-owned
    ///     <c>state.Mana</c> at the moment the command is actually drained. See the
    ///     items-skills-fenrir-self-critique finding this fixes.
    /// </remarks>
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
