using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class SupportSkillTimeUpRatioMaintenanceSystem(DirtyTracker<int> dirtyTracker) : ISimulationSystem
{
    private const short Zone126MapId = 126;

    private const int Zone126TimeStatSort = 22;

    private const int DoubleBuffTimeStatSort = 42;

    private const int PremiumStatSort = 116;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed, zone.MapId);
    }

    private void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed, short mapId)
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
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = DoubleBuffTimeStatSort, Value = state.BuffX2Time, Value2 = 0 });

            if (state.BuffX2Time == 0)
                recomputeNeeded = true;
        }

        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (state.PremiumExpireUtc != 0 && state.PremiumExpireUtc < nowUnixSeconds)
        {
            state.PremiumExpireUtc = 0;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            recomputeNeeded = true;

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = PremiumStatSort, Value = 0, Value2 = 0 });

            NotifyZone126CreditExhaustedOnPremiumExpiry(state, mapId);
        }

        if (recomputeNeeded)
            state.RecomputeSupportSkillTimeUpRatio();
    }

    private static void NotifyZone126CreditExhaustedOnPremiumExpiry(PlayerRuntimeState state, short mapId)
    {
        if (mapId != Zone126MapId || state.UserSort >= 1 || state.Zone126Time != 0)
            return;

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = Zone126TimeStatSort, Value = 0, Value2 = 0 });
    }
}
