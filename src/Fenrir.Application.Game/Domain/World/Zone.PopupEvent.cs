using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private readonly PopupEventRewardSystem? _popupEventRewardSystem =
        simulationSystems.OfType<PopupEventRewardSystem>().FirstOrDefault();

    private void NotifyPopupEventPvpKill(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        _popupEventRewardSystem?.NotifyPvpKill(this, attackerState, defenderState);
    }

    public void NotifyPopupEventMonsterKill(PlayerRuntimeState killer, bool dropEligible)
    {
        _popupEventRewardSystem?.NotifyMonsterKill(this, killer, dropEligible);
    }
}
