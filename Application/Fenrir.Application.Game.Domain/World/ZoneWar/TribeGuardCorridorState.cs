namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The live <c>mTribeGuardState[4][4]</c> table: for each of the 4 tribes' own corridors, whether each of
///     its 4 checkpoint segments is currently open (passable) or closed (blocking). One process-wide instance
///     is shared by every <see cref="Zone" /> actor -- the direct analog of the legacy's single named
///     shared-memory mapping common to the center process and every zone process
///     (<c>Server/Header/S15_MyShare.cpp:47-68,101-109</c>): there is exactly one live copy, never a per-zone
///     or per-shard copy that needs explicit synchronization.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/DEFINE.h:309-310 (<c>MAX_TRIBE_NUM</c>/<c>MAX_TRIBE_GUARD_NUM</c> = 4,
///     the table dimensions) ; Server/ts25center/S07_MyGame01.cpp:82-88 (cluster-boot default: every segment
///     starts closed/blocking) ; Server/ts25center/S04_MyWork02.cpp:339-347 (the legacy zone-to-center push
///     that keeps center's own copy in sync -- redundant here, since Fenrir has no separate center process and
///     every zone actor already shares this one instance directly).
///     <para>
///         Never persisted to a database (contrast with the separate, DB-backed "hub zone siege winner"
///         concept, <see cref="WorldState.WorldStateService.SetZone038Winner" />) -- this table is reset to
///         fully-closed only by process cold start, and re-derived from live guard-post creature counts by
///         <see cref="TribeGuardCorridorStateDerivationSystem" /> thereafter.
///     </para>
/// </remarks>
public sealed class TribeGuardCorridorState
{
    public const int TribeCount = 4;
    public const int SegmentCount = 4;

    private readonly bool[,] _open = new bool[TribeCount, SegmentCount]; // default false = closed, the boot state
    private readonly Lock _lock = new();

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
