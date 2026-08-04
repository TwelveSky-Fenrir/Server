using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

public readonly record struct MountAnimalInfo(int Slot, int Activity, int Exp)
{
    public const int ActiveCompanionSlotBase = 10;

    public const int ActiveCompanionSlotCount = 10;

    public static bool TryResolve(int animalIndex, ImmutableArray<int> mountActivity,
        ImmutableArray<int> mountAccumulatedExp, out MountAnimalInfo animal)
    {
        if (animalIndex < ActiveCompanionSlotBase ||
            animalIndex >= ActiveCompanionSlotBase + ActiveCompanionSlotCount)
        {
            animal = default;
            return false;
        }

        var slot = animalIndex - ActiveCompanionSlotBase;
        if (mountActivity.IsDefault || mountAccumulatedExp.IsDefault ||
            slot >= mountActivity.Length || slot >= mountAccumulatedExp.Length)
        {
            animal = default;
            return false;
        }

        animal = new MountAnimalInfo(slot, MountActivityExpCodec.ClampActivity(mountActivity[slot]),
            MountActivityExpCodec.ClampExp(mountAccumulatedExp[slot]));
        return true;
    }

    public static MountAnimalInfo Resolve(int animalIndex, ImmutableArray<int> mountActivity,
        ImmutableArray<int> mountAccumulatedExp)
    {
        return TryResolve(animalIndex, mountActivity, mountAccumulatedExp, out var animal) ? animal : default;
    }
}
