namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The live <c>mTribeGuardState[4][4]</c> table: for each of the 4 tribes' own corridors, whether each of
///     its 4 checkpoint segments is currently open (passable) or closed (blocking). One instance is shared by
///     every <see cref="Zone" /> actor hosted in THIS running <c>GameServer</c> PROCESS -- modeled after the
///     legacy's single named shared-memory mapping common to the center process and every zone process
///     (<c>Server/Header/S15_MyShare.cpp:47-68,101-109</c>), which was genuinely one copy for the WHOLE
///     cluster. See the CROSS-SHARD GAP paragraph below: that cluster-wide guarantee does NOT carry over to
///     Fenrir's per-process singleton once map-based sharding splits the corridor/hub zones across more than
///     one live shard.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/DEFINE.h:309-310 (<c>MAX_TRIBE_NUM</c>/<c>MAX_TRIBE_GUARD_NUM</c> = 4,
///     the table dimensions) ; Server/ts25center/S07_MyGame01.cpp:82-88 (cluster-boot default: every segment
///     starts closed/blocking) ; Server/ts25center/S04_MyWork02.cpp:339-347 (the legacy zone-to-center push
///     that keeps center's own copy in sync -- this push has NO Fenrir equivalent; see the cross-shard gap
///     note below, corrected from a prior version of this comment that called it "redundant here").
///     <para>
///         Never persisted to a database (contrast with the separate, DB-backed "hub zone siege winner"
///         concept, <see cref="WorldState.WorldStateService.SetZone038Winner" />) -- this table is reset to
///         fully-closed only by process cold start, and re-derived from live guard-post creature counts by
///         <see cref="TribeGuardCorridorStateDerivationSystem" /> thereafter.
///     </para>
///     <para>
///         CROSS-SHARD GAP (Fenrir architecture note, not a legacy citation -- flagged by the
///         center-world-authority "process-wide-singleton-not-cross-shard-synced" review, 2026-07): this type
///         is registered <c>AddSingleton</c> in
///         <c>Fenrir.Application.Game.Hosting/Extensions/HostingServiceCollectionExtensions.cs</c>, i.e. one
///         instance per running <c>GameServer</c> process, not one per cluster. <c>GameServer</c> is sharded
///         by disjoint map sets (<c>admin.ShardMapAssignments</c>), so the shared hub zone and its sixteen
///         corridor zones are not guaranteed to all be hosted by the same shard. If they are ever split
///         across more than one live shard, <see cref="TribeGuardCorridorStateDerivationSystem" /> (writer,
///         runs on whichever shard hosts the checkpoint's OWNING zone) and <c>TribeGuardCorridorGate</c>
///         (reader, invoked on whichever shard hosts the zone-move request's DESTINATION zone) would silently
///         read and write two independent per-process copies with no synchronization between them -- no
///         exception, just a possibly-stale passability answer with nothing on the wire indicating a scope
///         boundary was crossed. No compensating behavior is designed or implemented here; unlike
///         <see cref="Social.Party.PartyIdentityResolver" />'s documented same-class-of-gap fallback for party
///         leader name resolution, this component has no fallback at all. The only present-day mitigation is
///         operational -- keeping the hub zone and all sixteen corridor zones co-located on one shard via
///         <c>admin.ShardMapAssignments</c> -- and that co-location is NOT enforced by any boot-time guard
///         the way <c>ShardPartitionGuard</c> enforces disjointness, so a future re-partitioning could violate
///         it silently. This is an open, unaddressed gap, not a verified-safe design; resolving it (e.g. a
///         cross-shard broadcast, a DB-backed shared table, or a boot-time co-location guard) is left to a
///         follow-up pass.
///     </para>
/// </remarks>
public sealed class TribeGuardCorridorState
{
    public const int TribeCount = 4;
    public const int SegmentCount = 4;
    private readonly Lock _lock = new();

    private readonly bool[,] _open = new bool[TribeCount, SegmentCount]; // default false = closed, the boot state

    /// <summary>True (open/passable) or false (closed/blocking) for one tribe's own checkpoint segment.</summary>
    public bool IsOpen(byte tribeId, byte segmentIndex)
    {
        ValidateTribeId(tribeId);
        ValidateSegmentIndex(segmentIndex);

        lock (_lock)
        {
            return _open[tribeId, segmentIndex];
        }
    }

    /// <summary>
    ///     Sets one segment's passability. Returns false (a no-op) when the requested state already matches --
    ///     "if the segment's state already matches the just-computed result, nothing changes (no redundant
    ///     notification is sent)". Fenrir has no separate center hop left to skip (see class remarks), so the
    ///     returned bool exists purely for callers/tests that want to observe an actual transition.
    /// </summary>
    public bool TrySetOpen(byte tribeId, byte segmentIndex, bool isOpen)
    {
        ValidateTribeId(tribeId);
        ValidateSegmentIndex(segmentIndex);

        lock (_lock)
        {
            if (_open[tribeId, segmentIndex] == isOpen)
                return false;

            _open[tribeId, segmentIndex] = isOpen;
            return true;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    private static void ValidateSegmentIndex(byte segmentIndex)
    {
        if (segmentIndex >= SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex,
                $"SegmentIndex must be 0-{SegmentCount - 1}.");
    }
}
