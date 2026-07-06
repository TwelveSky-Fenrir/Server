using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Post-death territorial revive-eligibility recheck (side effect 1) and <c>mProtect_ReviveHack</c>
///     50-tick anti-abuse force-quit safety valve (side effect 2) -- <c>CheckAvatarObject</c>'s own per-tick
///     pass over every connected avatar (S07_MyGame04.cpp:6-37,218-224), the recheck/force-quit themselves at
///     S07_MyGame04.cpp:257-332.
/// </summary>
/// <remarks>
///     Runs every legacy tick for every player, dead or not (matching the legacy's own unconditional per-avatar
///     pass) -- a living player's <see cref="PlayerRuntimeState.ReviveHackFlag" /> is always false by
///     construction (armed only in <see cref="Zone.ApplyDeath" />, cleared together with
///     <see cref="PlayerRuntimeState.IsDead" /> in <see cref="Zone.GrantReviveEligibility" />), so the
///     force-quit check is a no-op for them. Registered last in the simulation pipeline
///     (<c>DomainServiceCollectionExtensions</c>) since it can end a session outright -- every other system's
///     per-tick mutation for a about-to-be-quit player should already have landed before that happens.
/// </remarks>
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

            // Evaluated unconditionally, immediately after the recheck above -- if that recheck just granted
            // eligibility this same tick, ReviveHackFlag is already false here and this does not fire.
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
