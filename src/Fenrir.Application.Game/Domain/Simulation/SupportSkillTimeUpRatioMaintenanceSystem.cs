using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class SupportSkillTimeUpRatioMaintenanceSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.SupportSkillTimeUpRatioAccrualTicks += legacyTicksElapsed;
        var minutesElapsed =
            state.SupportSkillTimeUpRatioAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.SupportSkillTimeUpRatioAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        var recomputeNeeded = false;
        if (state.BuffX2Time > 0)
        {
            state.BuffX2Time = Math.Max(0, state.BuffX2Time - minutesElapsed);
            if (state.BuffX2Time == 0)
                recomputeNeeded = true;
        }

        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (state.PremiumExpireUtc != 0 && state.PremiumExpireUtc < nowUnixSeconds)
        {
            state.PremiumExpireUtc = 0;
            recomputeNeeded = true;
        }

        if (recomputeNeeded)
            state.RecomputeSupportSkillTimeUpRatio();
    }
}
