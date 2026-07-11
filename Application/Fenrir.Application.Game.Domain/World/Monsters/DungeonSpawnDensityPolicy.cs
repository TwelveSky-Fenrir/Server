namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Legacy's dungeon field-monster spawn-count density bump: a one-shot, boot-time transformation of a
///     zone's <c>world.MonsterSpawnRegions</c> table that, only on a "dungeon" zone-server process, forces any
///     row configured to summon 5-19 copies of a monster whose own id is below 500 up to a flat 20 copies
///     instead -- and, on the same process, doubles the whole-table capacity ceiling that determines whether
///     the (possibly now-bumped) table is accepted at all or discarded in its entirety.
/// </summary>
/// <remarks>
///     Réf. contract : <c>legacy-behavior-translator</c> wave-12 <c>A4-dungeon-density.md</c>, itself citing
///     <c>Server/ts25zone/S10_MySummon.cpp:544-598</c> (<c>LoadRegionInfo_1</c>; the density-bump loop is
///     <c>:557-573</c>, the whole-table capacity-abort check is <c>:575-580</c>) and
///     <c>Server/ts25zone/S02_MyServer.cpp:8-168</c> (the dungeon flag and the capacity-ceiling doubling, set
///     together at boot from a fixed, hardcoded list of physical zone-server process numbers).
///     <para>
///         <b>Pure, boot-time-only policy.</b> This class has no per-tick cost and touches no mutable state --
///         it exists purely so <see cref="MonsterSpawnScheduler.BuildState" /> can call one well-tested
///         function per row instead of inlining the four-condition gate, and one function for the
///         capacity-ceiling doubling, instead of re-deriving either from scratch at the call site.
///     </para>
///     <para>
///         <b>Whole-table capacity ceiling, not a per-row cap.</b> <see cref="ResolveTableCapacity" /> only
///         answers "what is this process's ceiling," not whether any given zone's total exceeds it -- the
///         accept-or-discard-the-whole-table decision itself stays at the call site
///         (<see cref="MonsterSpawnScheduler" />'s own <c>RegularMonsterTableCapacity</c>), matching the
///         contract's own framing of doubling as a precondition of the transformation, not part of it.
///     </para>
///     <para>
///         <b>Open questions preserved, not resolved here (see the wave-12 contract's own "Open questions"
///         section):</b> the specific literal thresholds 5, 19, 20, and 500 have no named constant, `#define`,
///         or comment anywhere in the reachable legacy source explaining their derivation -- genuinely
///         unrecoverable, not invented. Likewise unrecoverable: why the gate was changed from a monster
///         type/special-type classification check to a raw numeric-id threshold (only a commented-out prior
///         check remains as a trace), and the real-world identity/classification of the specific monster ids
///         the 500 threshold differentiates in shipped data.
///     </para>
///     <para>
///         <b>Step-2/step-4 resolution-skip divergence does not apply to Fenrir's architecture.</b> The
///         contract flags (as an explicitly unconfirmed, theoretical open question) that legacy's own
///         unresolvable-monster-row skip from the capacity-total accumulation, combined with that same row
///         still being iterated in a later per-instance timing pass sized exactly to the capacity ceiling,
///         could in theory write beyond that fixed-size timing table's bounds. Fenrir has no equivalent
///         fixed-size timing table at all -- <see cref="MonsterZoneSpawnState.Slots" />
///         is a plain <see cref="List{T}" /> sized exactly to what actually got resolved and accepted, and
///         (per <see cref="MonsterSpawnScheduler.BuildState" />'s own existing resolution loop) an
///         unresolvable-monster row never enters that list at all, in either pass -- so this specific overrun
///         risk has no Fenrir counterpart to reproduce or guard against.
///     </para>
/// </remarks>
public static class DungeonSpawnDensityPolicy
{
    /// <summary>
    ///     Lower bound (inclusive) of the configured-spawn-count band this bump applies to.
    ///     <c>Server/ts25zone/S10_MySummon.cpp:557-573</c>. Derivation of this specific literal is not
    ///     recoverable from <c>Server/</c> -- see this class's own remarks.
    /// </summary>
    public const int MinConfiguredCountForBump = 5;

    /// <summary>
    ///     Upper bound (inclusive) of the configured-spawn-count band this bump applies to. A row already at
    ///     or above this value is left untouched -- already at or above the density the bump would otherwise
    ///     force. <c>Server/ts25zone/S10_MySummon.cpp:557-573</c>.
    /// </summary>
    public const int MaxConfiguredCountForBump = 19;

    /// <summary>
    ///     The fixed literal a qualifying row's spawn count is overwritten to -- a hard reset, not a
    ///     multiplier and not an incremental adjustment. <c>Server/ts25zone/S10_MySummon.cpp:557-573</c>.
    /// </summary>
    public const int BumpedSpawnCount = 20;

    /// <summary>
    ///     Exclusive upper bound on the row's <i>resolved</i> monster id (re-read off the reference-table
    ///     entry the row's monster id looked up to, not the row's own original field) for the row to be
    ///     bump-eligible -- a resolved monster id of 500 or above is excluded even though it meets the
    ///     count-range gate. Confirmed to genuinely differentiate rows in at least one shipped
    ///     island-dungeon spawn-region file per the wave-12 contract's Edge cases section.
    ///     <c>Server/ts25zone/S10_MySummon.cpp:563-567</c>.
    /// </summary>
    public const int MonsterIdBumpCeilingExclusive = 500;

    /// <summary>
    ///     Legacy doubles a fixed set of object-slot capacity ceilings for a dungeon-flagged process at the
    ///     same boot step that sets the dungeon flag itself (<c>Server/ts25zone/S02_MyServer.cpp:149-168</c>)
    ///     -- a plain doubling, not some other multiplier, per the wave-12 contract's own Preconditions
    ///     wording ("doubles a fixed set of object-slot capacity ceilings").
    /// </summary>
    private const int CapacityMultiplierWhenDungeon = 2;

    /// <summary>
    ///     Resolves one <c>world.MonsterSpawnRegions</c> row's spawn count after the dungeon density bump.
    /// </summary>
    /// <param name="isDungeonZone">
    ///     Whether the zone-server process hosting this zone is dungeon-flagged. False on every non-dungeon
    ///     shard, in which case this method is a pure passthrough of <paramref name="configuredCount" />.
    /// </param>
    /// <param name="configuredCount">
    ///     The row's own configured spawn count (<c>world.MonsterSpawnRegions.Number</c>) before this
    ///     transformation, already clamped non-negative by the caller (legacy's own <c>mNumber</c> field is
    ///     read raw with no cited non-negative guard upstream of this transformation, so the caller -- not
    ///     this method -- owns that clamp; see <see cref="MonsterSpawnScheduler.BuildState" />).
    /// </param>
    /// <param name="resolvedMonsterId">
    ///     The row's monster id as re-read off the reference-table entry it resolved to (not the row's own
    ///     original field, though normally identical in well-formed data). Only meaningful -- and only ever
    ///     called by <see cref="MonsterSpawnScheduler.BuildState" /> -- for a row whose monster id already
    ///     resolved; an unresolvable row never reaches this method at all, matching the contract's own Edge
    ///     cases (an unresolvable row is excluded from the density-bump decision entirely, not merely from the
    ///     bump amount).
    /// </param>
    /// <returns>
    ///     <see cref="BumpedSpawnCount" /> when every one of the four gate conditions holds; otherwise
    ///     <paramref name="configuredCount" /> unchanged.
    /// </returns>
    public static int ResolveConfiguredSpawnCount(bool isDungeonZone, int configuredCount, int resolvedMonsterId)
    {
        if (!isDungeonZone)
            return configuredCount;

        if (configuredCount is < MinConfiguredCountForBump or > MaxConfiguredCountForBump)
            return configuredCount;

        if (resolvedMonsterId >= MonsterIdBumpCeilingExclusive)
            return configuredCount;

        return BumpedSpawnCount;
    }

    /// <summary>
    ///     Resolves the whole-table object-slot capacity ceiling a zone's accumulated spawn-count total is
    ///     checked against, doubling <paramref name="baseCapacity" /> when the process is dungeon-flagged.
    /// </summary>
    public static int ResolveTableCapacity(bool isDungeonZone, int baseCapacity)
    {
        return isDungeonZone ? baseCapacity * CapacityMultiplierWhenDungeon : baseCapacity;
    }
}
