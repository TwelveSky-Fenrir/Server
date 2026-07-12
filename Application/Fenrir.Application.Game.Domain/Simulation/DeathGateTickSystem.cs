using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class DeathGateTickSystem(WorldStateService worldState) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        List<PlayerRuntimeState>? toForceQuit = null;

        foreach (var state in zone.Players)
        {
            if (!state.IsDead && !state.ReviveHackFlag)
                continue;

            state.TicksSinceDeath += legacyTicksElapsed;

            if (state.IsDead && state.TicksSinceDeath >= SimulationClock.ReviveEligibilityLegacyTicks)
                ApplyReviveClear(zone, state);

            if (state.ReviveHackFlag && state.TicksSinceDeath >= SimulationClock.AntiAbuseForceQuitLegacyTicks)
                (toForceQuit ??= []).Add(state);
        }

        if (toForceQuit is null)
            return;

        foreach (var state in toForceQuit)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
    }

    private void ApplyReviveClear(Zone zone, PlayerRuntimeState state)
    {
        var outcome = ReviveEligibilityRules.Resolve(zone.MapId, state.Tribe, worldState.GetAllyOf(state.Tribe));

        switch (outcome)
        {
            case ReviveClearOutcome.ClearDeathWindowAndLock:
                zone.GrantReviveEligibility(state);
                break;
            case ReviveClearOutcome.ClearDeathWindowOnly:
                zone.ClearDeathWindowKeepLockArmed(state);
                break;
        }
    }
}
