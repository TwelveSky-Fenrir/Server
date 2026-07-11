using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeGuardRegionLayout
{

        public const int NormalGuardRegionLegacyStart = 3400;

        public const int TribeGuardRegionLegacyStart = 3500;

        public const int RegionSlotCount = 100;

        public const int SlotsPerTribeSlot = 5;

        public const int TribeSlotCount = 5;

        public const int MaxUsedSlots = TribeSlotCount * SlotsPerTribeSlot;

        public static int RelativeReservedIndex(int tribeSlotOrdinal, int postWithinSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tribeSlotOrdinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tribeSlotOrdinal, TribeSlotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(postWithinSlot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(postWithinSlot, SlotsPerTribeSlot);

        return SlotsPerTribeSlot * tribeSlotOrdinal + postWithinSlot;
    }

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
