namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Side effect §1 of the A4-summonguard contract -- the <c>tCheckFirst == true</c> (force-reset) region
///     wipe. When <c>MySummon::SummonGuard</c> is called in force-reset mode, "every object slot across the
///     entire hundred-slot target region ... is marked dead and stamped with the current tick as its death
///     time, before any spawning. This clears both live and dead prior occupants and resets their cooldown
///     baseline to now" (<c>Server/ts25zone/S10_MySummon.cpp:1809-1816</c>). Only after that wipe does the
///     spawn loop run, so every configured post is re-planted fresh -- a damaged-but-alive guard is reset to
///     full HP with a fresh unique number, not left as-is.
///     <para>
///         The four force-reset triggers are all the "forced" callers: map boot, the Zone038 victory transition,
///         and both Holy-Stone-Battle relay events ("hsb countdown"/"hsb start") -- see
///         <see cref="TribeGuardSpawner" />'s boot pass and its
///         <see cref="TribeGuardSpawner.ForceOrdinaryResummon" />/
///         <see cref="TribeGuardSpawner.ForceZone038WinnerResummon" />
///         entry points. The periodic every-twentieth-tick re-evaluation runs in cooldown mode instead
///         (<c>tCheckFirst == false</c>) and never wipes.
///     </para>
///     <para>
///         Fenrir has no separate "dead-but-slot-still-occupied" object state: a wiped guard is removed from the
///         zone's monster table outright via <see cref="Zone.DespawnMonsterSilently" /> (no loot, no death event,
///         no death broadcast), which is the exact observable equivalent of legacy marking the shared-memory slot
///         dead before overwriting it. Walking the whole hundred-slot region (not just the twenty-five ever
///         populated) is faithful to §1 and free: <see cref="Zone.DespawnMonsterSilently" /> on an empty slot is a
///         no-op, and only the family's own guards ever live inside its pool range, so nothing else is ever
///         touched.
///     </para>
/// </summary>
public static class TribeGuardForceResetSweep
{
    /// <summary>
    ///     Wipes every currently-live guard across <paramref name="poolServerIndexBase" />'s whole
    ///     <see cref="TribeGuardRegionLayout.RegionSlotCount" />-slot region, so the subsequent spawn pass plants
    ///     fresh guards into every configured slot. Tick-thread-owned caller only (invoked from inside
    ///     <see cref="TribeGuardSpawner" />'s force-first-pass path, which itself only runs on the zone's own
    ///     <see cref="Zone.Tick" />).
    /// </summary>
    /// <param name="zone">The zone whose monster table hosts this pool's guards.</param>
    /// <param name="poolServerIndexBase">
    ///     The synthetic per-family pool base -- <c>TribeGuardSpawner.OrdinaryPoolServerIndexBase</c> for
    ///     the normal-guard region, <c>TribeGuardSpawner.Zone038WinnerPoolServerIndexBase</c> for the tribe-guard
    ///     region. A slot's
    ///     full server index is this base plus its region-relative reserved index (see
    ///     <see cref="TribeGuardRegionLayout" />).
    /// </param>
    /// <returns>How many live guards were wiped (0 on an empty region) -- useful for tests and telemetry.</returns>
    public static int Wipe(Zone zone, int poolServerIndexBase)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var wiped = 0;
        for (var relativeIndex = 0; relativeIndex < TribeGuardRegionLayout.RegionSlotCount; relativeIndex++)
        {
            var serverIndex = poolServerIndexBase + relativeIndex;
            if (!zone.TryGetMonster(serverIndex, out _))
                continue;

            zone.DespawnMonsterSilently(serverIndex);
            wiped++;
        }

        return wiped;
    }
}
