using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct TribeGuardCorridorChain(ImmutableArray<short> Zones);

public sealed class TribeGuardCorridorCatalog
{
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

    public short HubZoneId { get; }

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

    public int? GetOriginSegmentIndex(byte tribeId, short zoneId)
    {
        if (zoneId == HubZoneId)
            return -1;

        if (!_chainsByTribe.TryGetValue(tribeId, out var chain))
            return null;

        var index = chain.Zones.IndexOf(zoneId);
        return index < 0 ? null : index;
    }

    public IReadOnlyList<(byte TribeId, byte SegmentIndex)> GetSegmentsOwnedByZone(short zoneId)
    {
        return _segmentsOwnedByZone.TryGetValue(zoneId, out var owned) ? owned : [];
    }

    public bool TryGetGuardPostSlots(byte tribeId, byte segmentIndex, out ImmutableArray<int> slots)
    {
        return _guardPostSlotsBySegment.TryGetValue((tribeId, segmentIndex), out slots);
    }
}
