using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeGuardCorridorCatalogFactory
{
    public const short HubZoneId = 38;

    private static readonly ImmutableArray<short> Tribe0Chain = [4, 3, 2, 1];

    private static readonly ImmutableArray<short> Tribe1Chain = [9, 8, 7, 6];

    private static readonly ImmutableArray<short> Tribe2Chain = [14, 13, 12, 11];

    private static readonly ImmutableArray<short> Tribe3Chain = [143, 142, 141, 140];

    public static TribeGuardCorridorCatalog BuildLive()
    {
        var chains = new Dictionary<byte, TribeGuardCorridorChain>
        {
            [0] = new(Tribe0Chain),
            [1] = new(Tribe1Chain),
            [2] = new(Tribe2Chain),
            [3] = new(Tribe3Chain)
        }.ToImmutableDictionary();

        return new TribeGuardCorridorCatalog(
            HubZoneId,
            chains,
            BuildGuardPostSlots());
    }

    private static ImmutableDictionary<(byte TribeId, byte SegmentIndex), ImmutableArray<int>> BuildGuardPostSlots()
    {
        var builder = ImmutableDictionary.CreateBuilder<(byte TribeId, byte SegmentIndex), ImmutableArray<int>>();

        for (byte tribeId = 0; tribeId < TribeGuardCorridorState.TribeCount; tribeId++)
        for (byte segmentIndex = 0; segmentIndex < TribeGuardCorridorState.SegmentCount; segmentIndex++)
        {
            var firstSlot = tribeId * GuardPostDefinition.SlotsPerPost;
            var slots = ImmutableArray.CreateBuilder<int>(GuardPostDefinition.SlotsPerPost);
            for (var offset = 0; offset < GuardPostDefinition.SlotsPerPost; offset++)
                slots.Add(TribeGuardSpawner.OrdinaryPoolServerIndexBase + firstSlot + offset);

            builder[(tribeId, segmentIndex)] = slots.MoveToImmutable();
        }

        return builder.ToImmutable();
    }
}
