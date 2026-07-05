using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     A Fenrir simplification of the legacy's per-pet-item growth counter -- tracked per character
    ///     instead of per item instance. Reset to the newly-equipped pet's base tier whenever the Equipment
    ///     container's pet slot changes to a different item id.
    /// </summary>
    public int PetGrowth { get; set; }

    /// <summary>
    ///     0-100 activity -- decays -1 every 30s while a pet is equipped and not already at 0. Gates the
    ///     attack contribution only; Life/Mana/Defense contributions do NOT gate on activity (verified).
    /// </summary>
    public byte PetActivity { get; set; }

    /// <summary>
    ///     The ItemId last seen equipped in the pet slot -- lets <see cref="World.Zone" /> detect a pet swap (not just
    ///     any equipment change) to reset <see cref="PetGrowth" />/<see cref="PetActivity" />. 0 = no pet equipped.
    /// </summary>
    public int LastSeenPetItemId { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.PetActivitySystem" />'s own 30s decay cadence -- never read
    ///     by anything else.
    /// </summary>
    public int PetActivityDecayTicks { get; set; }

    /// <summary>
    ///     aAnimal[10] mount garage -- no acquisition path exists yet (only the unimplemented UseInventoryItem
    ///     mount-item family populates it per S04_MyWork03.cpp), so every slot stays 0 until that lands. A real
    ///     array, permanently empty for now, same posture as <see cref="MissionJoinWar" />.
    /// </summary>
    public ImmutableArray<int> MountGarage { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     aAnimalIndex. -1 = none selected, 0-9 = selected garage slot, 10-19 = actively mounted (slot + 10, legacy's
    ///     own offset encoding).
    /// </summary>
    public int AnimalIndex { get; set; } = -1;

    /// <summary>aAnimalNumber -- the currently-mounted animal id feeding combat stats/broadcast. 0 = not mounted.</summary>
    public int AnimalNumber { get; set; }

    /// <summary>aAnimalAbsorbState (0/1) -- CZ_ANIMAL_ABSORB_SEND toggle.</summary>
    public int AnimalAbsorbState { get; set; }

    /// <summary>
    ///     aAnimalTime -- gates CZ_ANIMAL_STATE_SEND case 3 (mount), &gt;= 1 required. Same "real but currently
    ///     unreachable" posture as <see cref="MissionJoinWar" />: no acquisition path exists yet, so this stays 0.
    /// </summary>
    public int AnimalTime { get; set; }

    /// <summary>
    ///     aAnimalAbsorbTime -- gates CZ_ANIMAL_ABSORB_SEND case 1 (enable absorb), &gt;= 1 required. Same posture as
    ///     <see cref="AnimalTime" />.
    /// </summary>
    public int AnimalAbsorbTime { get; set; }

    /// <summary>
    ///     aCostume[10] wardrobe -- same "no acquisition path yet" posture as <see cref="MountGarage" />. A
    ///     slot's occupancy is simplified here to "non-zero" rather than replicating the legacy's ~300-entry
    ///     IsValidCostume item-id whitelist (Server/Header/function.h): no code path grants a costume yet, so
    ///     the simplification never diverges from that table in practice today.
    /// </summary>
    public ImmutableArray<int> CostumeWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     aCostumeIndex. Same offset-encoding convention as <see cref="AnimalIndex" /> (-1 none, 0-9 selected, 10-19
    ///     worn).
    /// </summary>
    public int CostumeIndex { get; set; } = -1;

    /// <summary>aCostumeNumber -- the currently-worn costume id. 0 = none worn.</summary>
    public int CostumeNumber { get; set; }

    /// <summary>aCostumeState -- CZ_COSTUME_STATE2_SEND visibility toggle (0/1).</summary>
    public int CostumeState { get; set; }

    /// <summary>
    ///     aAction.aPetSort -- last accepted CZ_UPDATE_PET_ACTION_SEND's pet sub-fields. The legacy handler has
    ///     no reply/broadcast of its own; the update rides along on the next full avatar rebroadcast instead
    ///     (<see cref="Zone" />'s shared rebroadcast-snapshot builder still needs wiring to surface these --
    ///     tracked here, not yet plumbed into that shared path).
    /// </summary>
    public int PetActionSort { get; set; }

    public float PetActionFront { get; set; }
    public float PetActionLocationX { get; set; }
    public float PetActionLocationY { get; set; }
    public float PetActionLocationZ { get; set; }
    public float PetActionTargetLocationX { get; set; }
    public float PetActionTargetLocationY { get; set; }
    public float PetActionTargetLocationZ { get; set; }

    /// <summary>
    ///     aRuneSystem[4] -- ItemId currently socketed per rune slot (93514-93517 family, index-aligned), 0 =
    ///     empty. Session-only, no persisted column exists yet -- same posture as <see cref="MountGarage" />.
    /// </summary>
    public ImmutableArray<int> RuneSystem { get; set; } = [0, 0, 0, 0];

    /// <summary>
    ///     aRuneSystemStat[4] -- the socketed rune's raw packed enchant/combine/refine/socket int (<c>ItemValueCodec</c>
    ///     -encoded), paired 1:1 with <see cref="RuneSystem" />.
    /// </summary>
    public ImmutableArray<int> RuneSystemStat { get; set; } = [0, 0, 0, 0];

    /// <summary>
    ///     aStellarCore[10] wardrobe -- same "no acquisition path yet" posture as <see cref="CostumeWardrobe" />:
    ///     no code path grants a stellar core yet, so every slot stays 0 until that lands.
    /// </summary>
    public ImmutableArray<int> StellarCoreWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     aStellarCoreIndex. Same offset-encoding convention as <see cref="CostumeIndex" /> (-1 none, 0-9 selected,
    ///     10-19 worn).
    /// </summary>
    public int StellarCoreIndex { get; set; } = -1;

    /// <summary>aStellarCoreNumber -- the currently-worn stellar core id feeding stats/broadcast. 0 = none worn.</summary>
    public int StellarCoreNumber { get; set; }
}
