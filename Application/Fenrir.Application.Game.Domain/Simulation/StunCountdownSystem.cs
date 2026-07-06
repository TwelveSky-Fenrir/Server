using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Stun/knockdown duration countdown (<c>AVATAR_OBJECT::Update</c>, <c>S07_MyGame04.cpp:438-459</c>): every
///     ~1 s (2 legacy ticks, <see cref="SimulationClock.StunCountdownLegacyTicks" />), a stunned character's
///     remaining duration decrements by one; reaching zero clears the stun and reverts the character to idle --
///     entirely time-driven, no client request involved.
/// </summary>
/// <remarks>
///     Unlike <see cref="PetActivitySystem" />'s single-decrement-per-call accumulator, this fully catches up a
///     multi-unit gap in one <see cref="Simulate" /> call (integer division of the accumulated legacy ticks) --
///     both are legitimate translations of "a burst greater than 1 means the host stalled and the system must
///     catch up" (<see cref="ISimulationSystem" />'s own contract); this one is chosen here because a stunned
///     character's countdown reaching exactly zero has an observable side effect (the stun clears) that must
///     not be delayed by a stalled host.
/// </remarks>
public sealed class StunCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (!state.IsStunned)
                continue;

            state.StunCountdownAccumulatorTicks += legacyTicksElapsed;
            var secondsElapsed = state.StunCountdownAccumulatorTicks / SimulationClock.StunCountdownLegacyTicks;
            if (secondsElapsed <= 0)
                continue;

            state.StunCountdownAccumulatorTicks -= secondsElapsed * SimulationClock.StunCountdownLegacyTicks;
            state.StunDurationSeconds -= secondsElapsed;

            if (state.StunDurationSeconds <= 0)
                zone.ClearStun(state);
        }
    }
}
