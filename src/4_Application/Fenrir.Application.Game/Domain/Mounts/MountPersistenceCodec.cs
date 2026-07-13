using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     Bridges the persisted character-row mount block and the 10-slot runtime mount arrays. The Fenrir schema
///     stores a single mount (legacy <c>aAnimal[0]</c>/<c>aAnimalExpActivity[0]</c>/<c>aAnimalPower[0]</c> plus
///     the <c>aAnimalIndex</c> pointer and <c>aAnimalTime</c> timer), so both hydration (world entry) and
///     re-encode (write-behind flush) target garage slot 0. Decode digit expansion itself lives in
///     <see cref="MountPowerCodec.WithSlotDigits" /> (packed exp/activity in <see cref="MountActivityExpCodec" />);
///     this type owns only the slot-0 assumption, the mounted-range test, and the flush-side re-encode.
/// </summary>
public static class MountPersistenceCodec
{
    /// <summary>The single garage slot the character row persists (the mounted/selected mount).</summary>
    public const int PersistedGarageSlot = 0;

    /// <summary>
    ///     True when an <c>aAnimalIndex</c> pointer denotes a currently-mounted state (10..19); anything below 10
    ///     or at/above 20 means no mount is ridden.
    /// </summary>
    public static bool IsMounted(int animalIndex)
    {
        return animalIndex >= MountAnimalInfo.ActiveCompanionSlotBase &&
               animalIndex < MountAnimalInfo.ActiveCompanionSlotBase + MountAnimalInfo.ActiveCompanionSlotCount;
    }

    /// <summary>
    ///     Re-encodes the runtime slot-0 activity/accumulated-exp back into the packed <c>aAnimalExpActivity</c>
    ///     word for persistence (activity*1e6 + exp, each clamped to its legacy ceiling).
    /// </summary>
    public static int EncodeExpActivity(ImmutableArray<int> mountActivity, ImmutableArray<int> mountAccumulatedExp)
    {
        return MountActivityExpCodec.Pack(mountActivity[PersistedGarageSlot],
            mountAccumulatedExp[PersistedGarageSlot]);
    }

    /// <summary>
    ///     Re-encodes the runtime slot-0 rolled-attribute digits back into the packed <c>aAnimalPower</c> word
    ///     for persistence.
    /// </summary>
    public static int EncodePower(ImmutableArray<int> mountRolledAttributes)
    {
        return MountPowerCodec.EncodeSlot(mountRolledAttributes, PersistedGarageSlot);
    }
}
