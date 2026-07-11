using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe home for the Zone195 "Nok-San" solo-capture stone state -- the per-slot
///     "owner" array and the per-tribe "stones held" count array the legacy modelled as cluster-wide
///     WORLD_INFO shared across every zone process (Server/ts25zone/S07_MyGame01.cpp:8399,8548-8574). Same
///     "slice of the ts25center WORLD_INFO singleton with no backing table yet" posture as
///     <see cref="ZoneCenterSiegeState" /> (which this class sits alongside): the legacy never persisted these
///     fields to SQL either -- they lived only in the one in-memory struct, rebuilt from zero on every center
///     restart -- so no write-behind host is wired for this class. A future feature that needs the stone
///     ownership to survive a shard restart needs its own schema + repository from a database-engineer
///     workflow first.
/// </summary>
/// <remarks>
///     <para>
///         CROSS-SHARD SCOPE GAP (same shape as <see cref="ZoneCenterSiegeState" />'s own remarks): this
///         in-memory instance is per-process, not per-cluster. The legacy Nok-San state lived on the single
///         ts25center hub every zone process reached, so a capture flip on one server was visible to every
///         other. Fenrir shards <see cref="ZoneRegistry" /> disjointly by map, so a flip committed here is
///         only visible to THIS shard's own hosted maps/players until a cross-shard relay is wired for it
///         (the same <c>IRvrSiegeEventRelayQueue</c> precedent <see cref="ZoneEventBroadcaster" /> and
///         <see cref="ZoneCenterBroadcastIngestor" /> already use for the tribe-symbol/Zone049 families).
///         Deferred, not silently ignored -- see this cluster's report to the calling workflow.
///     </para>
///     <para>
///         Bounds: the owner array is <see cref="StoneSlotCount" /> (9) wide
///         (<c>MAX_ZONE_195_STONE_STATE</c>, Server/Header/Protocol/STRUCT.h:589); the stones-held array is
///         per tribe (<see cref="TribeCount" /> = 4, <c>MAX_TRIBE_NUM</c>, Server/Header/Protocol/DEFINE.h:309)
///         and each tribe's count is capped at <see cref="MaxStonesPerTribe" /> (4,
///         Server/ts25zone/S07_MyGame01.cpp:8576-8577). Owner slot value 0 means "uncaptured"; any other
///         value is the owning tribe number PLUS one (1-4), matching the legacy encoding
///         (Server/ts25zone/S07_MyGame01.cpp:8570-8571).
///     </para>
/// </remarks>
public sealed class Zone195NokSanState
{
    /// <summary>Per-slot owner array width -- <c>MAX_ZONE_195_STONE_STATE</c> (Server/Header/Protocol/STRUCT.h:589).</summary>
    public const int StoneSlotCount = 9;

    /// <summary>Maximum stones a single tribe may hold -- capped on credit (Server/ts25zone/S07_MyGame01.cpp:8576-8577).</summary>
    public const int MaxStonesPerTribe = 4;

    /// <summary>
    ///     Per-stone additive damage bonus a stone-holding tribe's members gain against monsters
    ///     (stones-held x 100, Server/ts25zone/S07_MyGame02.cpp:2326-2331) -- read by the combat path via
    ///     <see cref="GetMonsterDamageBonus" />, NOT applied by this class.
    /// </summary>
    public const int MonsterDamageBonusPerStone = 100;

    /// <summary>
    ///     The small "capture spot" radius a challenger must stand inside, in world units
    ///     (Server/ts25zone/S07_MyGame01.cpp:1148-1151). Used as the <see cref="Zone195NokSanSite" /> default.
    /// </summary>
    public const float DefaultCaptureRadius = 12.5f;

    /// <summary>Number of tribes -- <c>MAX_TRIBE_NUM</c> (Server/Header/Protocol/DEFINE.h:309).</summary>
    public static readonly int TribeCount = WorldStateService.TribeCount;

    private readonly Lock _lock = new();

    /// <summary>Per-slot owner: 0 = uncaptured, otherwise owning tribe number + 1 (1-4).</summary>
    private readonly int[] _owners = new int[StoneSlotCount];

    /// <summary>Per-tribe count of stones currently held.</summary>
    private readonly int[] _stonesHeld = new int[TribeCount];

    public static bool IsValidSlot(int slotIndex)
    {
        return slotIndex is >= 0 && slotIndex < StoneSlotCount;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId is >= 0 && tribeId < TribeCount;
    }

    /// <summary>Raw owner slot value: 0 = uncaptured, otherwise owning tribe number + 1.</summary>
    public int GetOwner(int slotIndex)
    {
        ValidateSlot(slotIndex);
        lock (_lock)
        {
            return _owners[slotIndex];
        }
    }

    /// <summary>
    ///     The tribe currently owning <paramref name="slotIndex" />, or <see langword="null" /> when the slot
    ///     is uncaptured -- decodes the legacy "owner + 1" encoding back to a raw tribe number.
    /// </summary>
    public byte? GetOwningTribe(int slotIndex)
    {
        var raw = GetOwner(slotIndex);
        return raw == 0 ? null : (byte)(raw - 1);
    }

    public int GetStonesHeld(byte tribeId)
    {
        ValidateTribe(tribeId);
        lock (_lock)
        {
            return _stonesHeld[tribeId];
        }
    }

    /// <summary>
    ///     The combat-path consumer (Server/ts25zone/S07_MyGame02.cpp:2326-2331): while
    ///     <paramref name="tribeId" /> holds at least one stone, every attack its members make against a
    ///     monster adds (stones-held x <see cref="MonsterDamageBonusPerStone" />) to the damage dealt. Returns
    ///     0 when the tribe holds no stones. Read-only helper -- wiring this into the actual monster-damage
    ///     pipeline is a separate gameplay-domain change (see this cluster's report), this class only exposes
    ///     the value.
    /// </summary>
    public int GetMonsterDamageBonus(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            return 0;

        return GetStonesHeld(tribeId) * MonsterDamageBonusPerStone;
    }

    /// <summary>
    ///     Commits a capture flip as ONE atomic unit under the single lock every reader uses
    ///     (Server/ts25zone/S07_MyGame01.cpp:8528-8577): the tribe that currently owns <paramref name="slotIndex" />
    ///     (if any -- an uncaptured slot has no prior owner and skips the debit) has its stones-held count
    ///     decremented and floored at zero; <paramref name="capturingTribe" />'s stones-held count is
    ///     incremented and then clamped to <see cref="MaxStonesPerTribe" />; and the slot's owner value is set
    ///     to <paramref name="capturingTribe" /> + 1. No torn intermediate state is observable to a concurrent
    ///     reader (a combat-path <see cref="GetStonesHeld" />/<see cref="GetMonsterDamageBonus" /> on another
    ///     shard-hosted map's own tick thread) -- the whole flip lands under one lock acquisition.
    /// </summary>
    public void CommitCapture(int slotIndex, byte capturingTribe)
    {
        ValidateSlot(slotIndex);
        ValidateTribe(capturingTribe);

        lock (_lock)
        {
            var priorOwner = _owners[slotIndex];
            if (priorOwner != 0)
            {
                // Owner encoding is "tribe + 1"; decode to index the debited tribe's own count. Floored at
                // zero -- any decrement that would land below one is clamped to zero (S07_MyGame01.cpp:8551-8552).
                var priorTribe = priorOwner - 1;
                if (priorTribe >= 0 && priorTribe < TribeCount)
                    _stonesHeld[priorTribe] = Math.Max(0, _stonesHeld[priorTribe] - 1);
            }

            _stonesHeld[capturingTribe] = Math.Min(MaxStonesPerTribe, _stonesHeld[capturingTribe] + 1);
            _owners[slotIndex] = capturingTribe + 1;
        }
    }

    /// <summary>
    ///     Takes ONE lock over the whole owner + stones-held state for the tSort 751 "Nok-San state" broadcast
    ///     payload (Server/ts25zone/S07_MyGame01.cpp:8580-8596) -- so the full per-tribe counts and per-slot
    ///     owners in a single broadcast frame can never be a torn mix of pre- and post-flip values from a
    ///     concurrent <see cref="CommitCapture" /> on another map's tick thread. Same "one lock over the whole
    ///     batch" posture as <c>TowerWarState.BuildStatusSnapshot</c>.
    /// </summary>
    public Zone195NokSanStateSnapshot Snapshot()
    {
        lock (_lock)
        {
            // A defensive copy of each backing array (never a wrapper) -- so a later CommitCapture mutation
            // can never show through a snapshot already handed to a broadcast.
            return new Zone195NokSanStateSnapshot(
                [.. _stonesHeld],
                [.. _owners]);
        }
    }

    private static void ValidateSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex,
                $"Zone195 stone slot must be 0-{StoneSlotCount - 1}.");
    }

    private static void ValidateTribe(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}

/// <summary>
///     Immutable snapshot of <see cref="Zone195NokSanState" /> for the tSort 751 cluster broadcast: the
///     complete per-tribe stones-held counts (length <see cref="Zone195NokSanState.TribeCount" />) and the
///     complete per-slot owner array (length <see cref="Zone195NokSanState.StoneSlotCount" />, each entry
///     0 = uncaptured or owning-tribe + 1). Server/ts25zone/S07_MyGame01.cpp:8580-8596.
/// </summary>
public readonly record struct Zone195NokSanStateSnapshot(
    ImmutableArray<int> StonesHeld,
    ImmutableArray<int> Owners);
