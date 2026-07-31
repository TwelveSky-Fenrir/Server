using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

public readonly record struct MountAnimalInfo(int Slot, int Activity, int Exp)
{
    public const int ActiveCompanionSlotBase = 10;

    public const int ActiveCompanionSlotCount = 10;

    public static MountAnimalInfo Resolve(int animalIndex, ImmutableArray<int> mountActivity,
        ImmutableArray<int> mountAccumulatedExp)
    {
        if (animalIndex < ActiveCompanionSlotBase || animalIndex >= ActiveCompanionSlotBase + ActiveCompanionSlotCount)
            return default;

        var slot = animalIndex - ActiveCompanionSlotBase;
        return new MountAnimalInfo(slot, mountActivity[slot], mountAccumulatedExp[slot]);
    }
}
