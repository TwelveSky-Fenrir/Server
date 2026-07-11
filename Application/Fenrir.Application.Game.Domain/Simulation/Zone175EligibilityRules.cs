using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public static class Zone175EligibilityRules
{
    public static bool IsPresent(bool isMovingZone, bool isHidden)
    {
        return !isMovingZone && !isHidden;
    }

    public static bool IsPresent(PlayerRuntimeState state)
    {
        return IsPresent(state.IsMovingZone, state.VisibleState == 0);
    }

    public static bool IsRewardEligible(bool isMovingZone, bool isHidden, bool isDead)
    {
        return IsPresent(isMovingZone, isHidden) && !isDead;
    }

    public static bool IsRewardEligible(PlayerRuntimeState state)
    {
        return IsRewardEligible(state.IsMovingZone, state.VisibleState == 0, state.IsDead);
    }
}
