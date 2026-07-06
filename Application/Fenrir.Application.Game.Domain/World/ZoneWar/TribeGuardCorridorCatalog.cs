using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The shape of one tribe's corridor: a strict linear chain of exactly <see cref="SegmentsPerTribe" /> zones
///     between the shared hub and that tribe's home zone, indexed 0 (the zone immediately past the hub-adjacent
///     checkpoint) to 3 (the tribe's own home zone). <see cref="Zones" />[i] is gated FOR ENTRY by checkpoint
///     segment i; the checkpoint's own guard posts live in whichever zone is immediately outside it
///     (the hub for segment 0, <see cref="Zones" />[i-1] for segment i &gt; 0) -- see
///     <see cref="TribeGuardCorridorCatalog.GetOwningZone" />.
/// </summary>
public readonly record struct TribeGuardCorridorChain(ImmutableArray<short> Zones);

/// <summary>
///     Structural catalog for the <c>mTribeGuardState[4][4]</c> corridor-gate table: which zone is which tribe's
///     corridor segment, which zone hosts (and re-derives) each checkpoint's guard-post liveness, and which
///     reserved monster-slot indices identify that checkpoint's
///     <see cref="TribeGuardCorridorStateDerivationSystem.GuardPostsPerSegment" />
///     guard-post creatures within the owning zone's own monster table.
///     <para>
///         Mirrors <see cref="GuardPostCatalog" />'s own documented posture: the actual
///         per-tribe zone-chain and per-segment guard-post slot assignments are hardcoded in the legacy
///         <c>MyUtil::WrapCheck</c> switch (<c>Server/ts25zone/S07_MyGame03.cpp:5721-5943</c>, cases 1-4, 6-9,
///         11-14, 140-143) and are NOT reproduced here -- populating a real catalog with the sixteen corridor
///         zone ids, the hub zone id, and each segment's five guard-post slot indices is a follow-up
///         data-gathering task against that switch. <see cref="Empty" /> makes every lookup miss, so every
///         consumer (<see cref="TribeGuardCorridorStateDerivationSystem" />, <see cref="TribeGuardCorridorGate" />)
///         degrades to a documented no-op/always-allow rather than a guess.
///     </para>
/// </summary>
public sealed class TribeGuardCorridorCatalog
{
    /// <summary>4 tribes x 4 segments = the sixteen corridor zones the contract refers to.</summary>
    public const int SegmentsPerTribe = 4;

    public static readonly TribeGuardCorridorCatalog Empty = new(
        0,
        ImmutableDictionary<byte, TribeGuardCorridorChain>.Empty,
        ImmutableDictionary<(byte TribeId, byte SegmentIndex), ImmutableArray<int>>.Empty);

    private readonly ImmutableDictionary<byte, TribeGuardCorridorChain> _chainsByTribe;

    private readonly ImmutableDictionary<(byte TribeId, byte SegmentIndex), ImmutableArray<int>>
        _guardPostSlotsBySegment;

    private readonly ImmutableDictionary<short, (byte TribeId, byte SegmentIndex)> _segmentByDestinationZone;
    private readonly ImmutableDictionary<short, ImmutableArray<(byte TribeId, byte SegmentIndex)>> _segmentsOwnedByZone;

    public TribeGuardCorridorCatalog(
        short hubZoneId,
        ImmutableDictionary<byte, TribeGuardCorridorChain> chainsByTribe,
        ImmutableDictionary<(byte TribeId, byte SegmentIndex), ImmutableArray<int>> guardPostSlotsBySegment)
    {
        HubZoneId = hubZoneId;
        _chainsByTribe = chainsByTribe;
        _guardPostSlotsBySegment = guardPostSlotsBySegment;

        var segmentByZone = ImmutableDictionary.CreateBuilder<short, (byte, byte)>();
        var ownedByZone = new Dictionary<short, List<(byte, byte)>>();

        foreach (var (tribeId, chain) in chainsByTribe)
            for (var segmentIndex = 0; segmentIndex < chain.Zones.Length; segmentIndex++)
            {
                var zoneId = chain.Zones[segmentIndex];
                segmentByZone[zoneId] = (tribeId, (byte)segmentIndex);

                var owningZone = segmentIndex == 0 ? hubZoneId : chain.Zones[segmentIndex - 1];
                if (!ownedByZone.TryGetValue(owningZone, out var owned))
                    ownedByZone[owningZone] = owned = [];
                owned.Add((tribeId, (byte)segmentIndex));
            }

        _segmentByDestinationZone = segmentByZone.ToImmutable();
        _segmentsOwnedByZone = ownedByZone.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutableArray());
    }

    /// <summary>The shared hub zone -- every tribe's segment-0 checkpoint is owned by this zone, not by any tribe's own chain.</summary>
    public short HubZoneId { get; }

    /// <summary>
    ///     True if <paramref name="zoneId" /> is one of the sixteen corridor zones (any tribe's chain slot),
    ///     with the owning tribe/segment index it corresponds to. False for the hub zone itself and for any
    ///     unrelated map -- both cases leave a zone-move request to this destination entirely unaffected by
    ///     this table (Edge case: "requests unrelated to the four tribes' corridors or the hub").
    /// </summary>
    public bool TryGetSegmentForDestinationZone(short zoneId, out byte tribeId, out byte segmentIndex)
    {
        if (_segmentByDestinationZone.TryGetValue(zoneId, out var segment))
        {
            (tribeId, segmentIndex) = segment;
            return true;
        }

        tribeId = 0;
        segmentIndex = 0;
        return false;
    }

    /// <summary>
    ///     -1 if <paramref name="zoneId" /> is the shared hub (the immediately-outside zone for segment 0),
    ///     the zone's own 0-3 segment index within <paramref name="tribeId" />'s own chain, or null if
    ///     <paramref name="zoneId" /> belongs to neither -- an origin the corridor-gate adjacency check must
    ///     reject outright (Edge case: "strict single-step adjacency for advances").
    /// </summary>
    public int? GetOriginSegmentIndex(byte tribeId, short zoneId)
    {
        if (zoneId == HubZoneId)
            return -1;

        if (!_chainsByTribe.TryGetValue(tribeId, out var chain))
            return null;

        var index = chain.Zones.IndexOf(zoneId);
        return index < 0 ? null : index;
    }

    /// <summary>
    ///     Every (tribe, segment) checkpoint that <paramref name="zoneId" /> re-derives on its own tick
    ///     (<see cref="TribeGuardCorridorStateDerivationSystem" />) -- the hub zone owns every tribe's segment 0
    ///     in one pass; a corridor zone at chain index i owns only that same tribe's segment i+1 (its home zone,
    ///     chain index 3, owns nothing). Empty for every other map -- the unconditional per-tick no-op.
    /// </summary>
    public IReadOnlyList<(byte TribeId, byte SegmentIndex)> GetSegmentsOwnedByZone(short zoneId)
    {
        return _segmentsOwnedByZone.TryGetValue(zoneId, out var owned) ? owned : [];
    }

    /// <summary>The five reserved monster-slot indices identifying this checkpoint's own guard posts, if configured.</summary>
    public bool TryGetGuardPostSlots(byte tribeId, byte segmentIndex, out ImmutableArray<int> slots)
    {
        return _guardPostSlotsBySegment.TryGetValue((tribeId, segmentIndex), out slots);
    }
}
