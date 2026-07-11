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
            if (state.IsDead)
            {
                state.TicksSinceDeath += legacyTicksElapsed;

                if (state.TicksSinceDeath >= SimulationClock.ReviveEligibilityLegacyTicks)
                    TryGrantReviveEligibility(zone, state);
            }

            if (state.ReviveHackFlag && state.TicksSinceDeath >= SimulationClock.AntiAbuseForceQuitLegacyTicks)
                (toForceQuit ??= []).Add(state);
        }

        if (toForceQuit is null)
            return;

        foreach (var state in toForceQuit)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.StateViolation);
    }

    private void TryGrantReviveEligibility(Zone zone, PlayerRuntimeState state)
    {
        var alliedTribe = worldState.GetAllyOf(state.Tribe);
        if (!ReviveEligibilityRules.IsEligible(zone.MapId, state.Tribe, alliedTribe))
            return;

        zone.GrantReviveEligibility(state);
    }
}
