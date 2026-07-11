using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The deterministic object-index <em>ownership</em> layout <c>MySummon::SummonGuard</c> writes its guard
///     posts into -- the piece the A4-summonguard contract calls out under Side effects ("Object-index ownership
///     is deterministic") and Preconditions/Edge cases ("Object regions unchanged for guard servers"), and which
///     <see cref="TribeGuardSpawner" />/<see cref="GuardPostCatalog" /> did not model until now.
///     <para>
///         Legacy carves the zone's shared monster-object array into two fixed hundred-slot regions -- the
///         <c>NORMAL</c> guard region at absolute index <see cref="NormalGuardRegionLegacyStart" /> and the
///         <c>TRIBE</c> guard region at <see cref="TribeGuardRegionLegacyStart" /> (both under the live
///         <c>USE_MON_ALLOC</c> path, <c>Server/ts25zone/S01_MainApplication.cpp:35-58</c>; none of the thirteen
///         guard-bearing servers ever have these rewritten, <c>Server/ts25zone/S02_MyServer.cpp:132-228</c>).
///         Within a region the write cursor "starts at the region's start index and advances by exactly five for
///         every one of the five tribe-slots -- whether that slot was populated, skipped for lack of
///         configuration, or skipped for a missing template", so tribe-slot <c>N</c> always owns the five-index
///         block starting at region-start plus five times <c>N</c>, and at most twenty-five slots
///         (<see cref="TribeSlotCount" /> x <see cref="SlotsPerTribeSlot" />) are ever used
///         (<c>Server/ts25zone/S10_MySummon.cpp:1817-1856</c>).
///     </para>
///     <para>
///         Fenrir keys guard monsters into a zone's own monster table under a synthetic per-family pool base
///         (<c>TribeGuardSpawner.OrdinaryPoolServerIndexBase</c> = 1_000_000 /
///         <c>TribeGuardSpawner.Zone038WinnerPoolServerIndexBase</c> = 1_001_000, each one thousand wide) rather
///         than the raw
///         3400/3500 absolute indices -- so the value actually stored on a
///         <see cref="GuardSlotCoordinate.ReservedSlotIndex" /> is <b>region-relative</b> (0..
///         <see cref="RegionSlotCount" />-1),
///         NOT the legacy absolute index; the pool base plays the role the 3400/3500 offset plays in the C++.
///         Folding 3400/3500 back into the reserved index would push a slot past its own thousand-wide pool
///         window and collide with the next family's pool, so the absolute constants below are kept purely for
///         citation fidelity and are never added into the C# index arithmetic.
///     </para>
/// </summary>
public static class TribeGuardRegionLayout
{
    /// <summary>
    ///     Legacy absolute start index of the <c>NORMAL</c> guard object region -- <c>START_NORMAL_GUARD_OBJECT_NUM</c>
    ///     (<c>Server/ts25zone/S01_MainApplication.cpp:35-58</c>). Documentation only; see the class remarks for
    ///     why the C# reserved index is region-relative instead.
    /// </summary>
    public const int NormalGuardRegionLegacyStart = 3400;

    /// <summary>
    ///     Legacy absolute start index of the <c>TRIBE</c> guard object region -- <c>START_TRIBE_GUARD_OBJECT_NUM</c>
    ///     (<c>Server/ts25zone/S01_MainApplication.cpp:35-58</c>). Documentation only; see the class remarks.
    /// </summary>
    public const int TribeGuardRegionLegacyStart = 3500;

    /// <summary>Each guard region is exactly one hundred object slots wide (same source).</summary>
    public const int RegionSlotCount = 100;

    /// <summary>
    ///     Every tribe-slot owns a fixed five-index block, and the write cursor advances by exactly this much per
    ///     tribe-slot regardless of whether that slot spawned anything (<c>S10_MySummon.cpp:1817-1856</c>). This is
    ///     the same figure as <see cref="GuardPostDefinition.SlotsPerPost" /> -- one post fills its owning
    ///     tribe-slot's block one-for-one.
    /// </summary>
    public const int SlotsPerTribeSlot = 5;

    /// <summary>
    ///     The number of tribe-slots a region is partitioned into: four tribe posts plus the optional fifth
    ///     "tribe-symbol area" post-group (Side effects / Edge cases of the contract). On the victory path only
    ///     tribe-slot zero is ever used, but the region is still partitioned the same way.
    /// </summary>
    public const int TribeSlotCount = 5;

    /// <summary>
    ///     The most object slots any single <c>SummonGuard</c> call ever touches --
    ///     <see cref="TribeSlotCount" /> x <see cref="SlotsPerTribeSlot" /> = 25, well inside the hundred-slot
    ///     region budget (<c>S10_MySummon.cpp:1817-1856</c>).
    /// </summary>
    public const int MaxUsedSlots = TribeSlotCount * SlotsPerTribeSlot;

    /// <summary>
    ///     The deterministic region-relative object index owned by post <paramref name="postWithinSlot" /> of
    ///     tribe-slot <paramref name="tribeSlotOrdinal" />: <c>5 * ordinal + post</c>. Because the block base is
    ///     purely a function of the tribe-slot ordinal, two different tribe-slots on the same map can never
    ///     collide on a reserved index -- unlike a naive "0..4 per post" numbering, where two posts on one map
    ///     would overwrite each other's slots (the second post's spawns would all see the first's guards already
    ///     alive and skip).
    /// </summary>
    /// <param name="tribeSlotOrdinal">Which of the five tribe-slots owns this block -- 0..<see cref="TribeSlotCount" />-1.</param>
    /// <param name="postWithinSlot">Which of the five posts inside that block -- 0..<see cref="SlotsPerTribeSlot" />-1.</param>
    public static int RelativeReservedIndex(int tribeSlotOrdinal, int postWithinSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tribeSlotOrdinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tribeSlotOrdinal, TribeSlotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(postWithinSlot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(postWithinSlot, SlotsPerTribeSlot);

        return SlotsPerTribeSlot * tribeSlotOrdinal + postWithinSlot;
    }

    /// <summary>
    ///     Builds the <see cref="GuardSlotCoordinate" /> array for one post, stamping each supplied coordinate with
    ///     its deterministic, collision-free <see cref="GuardSlotCoordinate.ReservedSlotIndex" /> from
    ///     <see cref="RelativeReservedIndex" />. This is the factory a future per-map/tribe data-population pass
    ///     (against <c>S10_MySummon.cpp:1339-1741, 1745-1799</c> -- Open question 6) must use when it replaces
    ///     <see cref="GuardPostCatalog.Empty" /> with the real coordinate table, so the populated posts inherit
    ///     the legacy object-index ownership rather than a hand-numbered scheme that risks the same-map collision
    ///     described on <see cref="RelativeReservedIndex" />.
    /// </summary>
    /// <param name="tribeSlotOrdinal">The tribe-slot this post owns -- 0..<see cref="TribeSlotCount" />-1.</param>
    /// <param name="coordinates">
    ///     The post's up-to-five world coordinates, in post order; at most <see cref="SlotsPerTribeSlot" /> may be
    ///     supplied.
    /// </param>
    public static ImmutableArray<GuardSlotCoordinate> BuildDeterministicSlots(int tribeSlotOrdinal,
        IReadOnlyList<(float X, float Y, float Z)> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(coordinates.Count, SlotsPerTribeSlot);

        var builder = ImmutableArray.CreateBuilder<GuardSlotCoordinate>(coordinates.Count);
        for (var post = 0; post < coordinates.Count; post++)
        {
            var (x, y, z) = coordinates[post];
            builder.Add(new GuardSlotCoordinate(x, y, z, RelativeReservedIndex(tribeSlotOrdinal, post)));
        }

        return builder.MoveToImmutable();
    }
}
