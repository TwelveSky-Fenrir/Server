using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Once-per-real-minute expired-mount auto-dismount for every mounted player in the zone: when a mount's
///     remaining rental time has run out while the character is still in the actively-mounted band, force a
///     dismount (<see cref="MountExpiryPolicy" />). Self-contained -- reads only the simulation clock and its own
///     <see cref="PlayerRuntimeState" /> fields, mutates only mount state, and defers the appearance broadcast /
///     ability recompute to the zone via <see cref="PlayerRuntimeState.MountAutoDismountPending" /> rather than
///     touching any zone broadcast channel. Same "self-contained, order relative to the other per-minute systems
///     doesn't matter" posture as <see cref="PetExpBoostCountdownSystem" />/<see cref="HoisundoCountdownSystem" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:6360-6363 (the expired-mount auto-dismount check itself).
///     <para>
///         DELIBERATELY OMITTED, FLAGGED: this system does NOT decrement <c>aAnimalTime</c>/<c>aAnimalAbsorbTime</c>.
///         The behavior contract carries the "per-minute absorb/expiry countdown" forward as UNCONFIRMED -- the
///         cited auto-dismount fires on avatar registration (a discrete event), and no cited line performs a
///         per-minute mount-timer decrement (the adjacent per-minute family lives at S07_MyGame04.cpp:787-823 for
///         <c>aAutoTime2</c>, but whether <c>aAnimalTime</c> decrements there is uncited). Per this codebase's
///         hard rule against inventing legacy numeric behavior, the decrement is left out pending a
///         <c>cpp-zone-gameplay-analyst</c> re-check; only the confirmed check-and-dismount is applied, on a
///         per-minute cadence chosen as the closest defensible home (it also subsumes the on-registration case
///         for a long-lived session). Until both a mount-time grant path AND a confirmed decrement path land, the
///         system is inert in practice -- a mount can only enter the band via op87 Sort 3 (which requires
///         <c>aAnimalTime</c> >= 1) and nothing lowers it, so <see cref="MountExpiryPolicy.IsExpiredWhileMounted" />
///         never holds. Same "real but currently unreachable" posture as the rest of the mount family.
///     </para>
///     <para>
///         Full-catch-up cadence (integer division), matching <see cref="PetExpBoostCountdownSystem" />: a stalled
///         host that accumulates several minutes' worth of ticks still evaluates the check once (the check is a
///         state test, not a per-minute decrement, so evaluating it N times in one pass is identical to once).
///     </para>
/// </remarks>
public sealed class MountExpiryCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.MountExpiryCountdownAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.MountExpiryCountdownAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.MountExpiryCountdownAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (!MountExpiryPolicy.IsExpiredWhileMounted(state.AnimalIndex, state.AnimalTime))
            return;

        state.AnimalIndex = MountExpiryPolicy.Dismounted(state.AnimalIndex);
        state.AnimalNumber = 0;
        state.AnimalAbsorbState = 0;
        state.MountAutoDismountPending = true;
    }
}
