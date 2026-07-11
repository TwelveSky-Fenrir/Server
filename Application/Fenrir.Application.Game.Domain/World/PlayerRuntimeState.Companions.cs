using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public int PetGrowth { get; set; }

    public byte PetActivity { get; set; }

    public int LastSeenPetItemId { get; set; }

    public int PetActivityDecayTicks { get; set; }

    public int PetExpX2Time { get; set; }

    public int PetExpX2TimeAccrualTicks { get; set; }

    public ImmutableArray<int> MountGarage { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public int AnimalIndex { get; set; } = -1;

    public int AnimalNumber { get; set; }

    public int AnimalAbsorbState { get; set; }

    public int AnimalTime { get; set; }

    public int AnimalAbsorbTime { get; set; }

    public ImmutableArray<int> MountAccumulatedExp { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public ImmutableArray<int> MountRolledAttributeTotal { get; set; } =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public ImmutableArray<int> MountRolledAttributes { get; set; } =
        Enumerable.Repeat(0, MountStateResolver.SlotCount * MountStateResolver.StatSlotCount).ToImmutableArray();

    public ImmutableArray<int> CostumeWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public int CostumeIndex { get; set; } = -1;

    public int CostumeNumber { get; set; }

    public int CostumeState { get; set; }

    public ImmutableArray<int> CostumeDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public int PetActionSort { get; set; }

    public float PetActionFront { get; set; }
    public float PetActionLocationX { get; set; }
    public float PetActionLocationY { get; set; }
    public float PetActionLocationZ { get; set; }
    public float PetActionTargetLocationX { get; set; }
    public float PetActionTargetLocationY { get; set; }
    public float PetActionTargetLocationZ { get; set; }

    public ImmutableArray<int> RuneSystem { get; set; } = [0, 0, 0, 0];

    public ImmutableArray<int> RuneSystemStat { get; set; } = [0, 0, 0, 0];

    public ImmutableArray<int> StellarCoreWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public int StellarCoreIndex { get; set; } = -1;

    public int StellarCoreNumber { get; set; }
}
