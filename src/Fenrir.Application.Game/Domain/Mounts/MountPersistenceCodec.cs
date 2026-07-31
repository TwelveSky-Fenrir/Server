using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountPersistenceCodec
{
    public const int PersistedGarageSlot = 0;

    public static bool IsMounted(int animalIndex)
    {
        return animalIndex >= MountAnimalInfo.ActiveCompanionSlotBase &&
               animalIndex < MountAnimalInfo.ActiveCompanionSlotBase + MountAnimalInfo.ActiveCompanionSlotCount;
    }

    public static int EncodeExpActivity(ImmutableArray<int> mountActivity, ImmutableArray<int> mountAccumulatedExp)
    {
        return MountActivityExpCodec.Pack(mountActivity[PersistedGarageSlot],
            mountAccumulatedExp[PersistedGarageSlot]);
    }

    public static int EncodePower(ImmutableArray<int> mountRolledAttributes)
    {
        return MountPowerCodec.EncodeSlot(mountRolledAttributes, PersistedGarageSlot);
    }
}
