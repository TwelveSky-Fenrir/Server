using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Stats;

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
    ///     Legacy <c>aPetExpX2Time</c> -- the "Pet EXP boost pill" consumable's (world.Items 1190/17035/8413)
    ///     running duration counter. While positive: doubles pet-kill EXP credited (see
    ///     <see cref="World.Zone.CreditPetGrowthFromMonsterKill" />) and pauses <see cref="Simulation.PetActivitySystem" />'s
    ///     30s activity decay entirely. Ticks down by (up to) 1 per elapsed real minute via
    ///     <see cref="Simulation.PetExpBoostCountdownSystem" /> -- but only while an equipped pet item is
    ///     present, and only while that pet's growth is still below the designer-facing 200% cap (see that
    ///     system's own remarks for both gates).
    /// </summary>
    /// <remarks>
    ///     In-memory only, same posture as <see cref="TowerCpMilestoneCounter" />'s own remarks: no
    ///     Characters/CharacterProgressTvp column backs this counter yet, so adding one is a product decision
    ///     outside this contract's scope, not something to invent here -- resets to 0 on a fresh world entry
    ///     (an in-process zone transfer still carries the live value through, same as <see cref="PetGrowth" />/
    ///     <see cref="PetActivity" />, via <see cref="World.ZoneTransfer.CreateEnterData" />). The one-shot grant
    ///     from consuming the pill marks Progression dirty (see <c>UseInventoryItemService</c>'s pet-exp-boost
    ///     branch) -- the same "rare, event-driven grant" posture <see cref="World.Zone.GrantWarPoints" />/
    ///     <see cref="World.Zone.GrantBloodPoints" /> already use despite an identical "not yet persisted"
    ///     status. The per-minute countdown and the 30s decay-pause do NOT mark dirty, matching
    ///     <see cref="Simulation.PlayTimeAccrualSystem" />'s own remarks for a guaranteed per-tick-per-player
    ///     counter with no persisted column to flush.
    /// </remarks>
    public int PetExpX2Time { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.PetExpBoostCountdownSystem" />'s own
    ///     once-per-real-minute cadence -- deliberately a separate counter from <see cref="PlayTimeAccrualTicks" />/
    ///     <see cref="SupportSkillTimeUpRatioAccrualTicks" /> even though all three share the same
    ///     120-legacy-tick (60s) period, matching <see cref="SupportSkillTimeUpRatioAccrualTicks" />'s own
    ///     remarks on why sharing one accumulator across independently-owned per-minute systems would
    ///     double-consume/desync them.
    /// </summary>
    public int PetExpX2TimeAccrualTicks { get; set; }

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
    ///     aAnimalExpActivity[10] -- per-garage-slot accumulated mount experience/activity gating
    ///     CZ_ANIMAL_STATE_SEND Sort 6 (Convert Attribute)'s ==MAX_MOUNT_EXP (100000) requirement, reset to 0
    ///     by a successful convert. Same "no acquisition/gain path yet" posture as <see cref="MountGarage" />:
    ///     every slot stays 0 until a mount-experience-gain system lands, so Sort 6 stays
    ///     reachable-but-never-completable by construction today, not by an explicit scope cut -- see
    ///     <see cref="MountStateResolver" />'s own remarks.
    /// </summary>
    public ImmutableArray<int> MountAccumulatedExp { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     aAnimalPower[10] -- per-garage-slot rolled-attribute total, gating Sort 6's &lt;25 cap and (once a
    ///     probability table is cataloged) accumulating that sub-action's own roll output on success. See
    ///     <see cref="MountStateResolver" />'s remarks for why the roll itself is not modeled yet.
    /// </summary>
    public ImmutableArray<int> MountRolledAttributeTotal { get; set; } =
        ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     Per-garage-slot, per-stat-slot (wire values 1-8, stored 0-based; flattened via
    ///     <see cref="MountStateResolver.AttributeIndex" />) rolled mount-attribute value -- Sort 7 (Delete)
    ///     zeroes the addressed entry; Sort 8 (Transfer) would move one once a transfer mechanic is
    ///     cataloged (see <see cref="MountStateResolver" />'s remarks). Not yet wired into
    ///     <see cref="StatCalculator" />: no combat-stat contribution formula for a mount's rolled
    ///     attributes has been cataloged either, same "not modeled" posture as <see cref="WarPoint" />.
    /// </summary>
    public ImmutableArray<int> MountRolledAttributes { get; set; } =
        Enumerable.Repeat(0, MountStateResolver.SlotCount * MountStateResolver.StatSlotCount).ToImmutableArray();

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
    ///     no reply/broadcast of its own; the update rides along on the next independently-triggered full
    ///     avatar-action rebroadcast instead. Every <see cref="Zone" /> call site that builds a fresh
    ///     <c>ActionInfo</c> for a broadcast reads this and its five siblings back via
    ///     <c>Zone.PetActionFieldsOf</c> rather than the empty/zero placeholder used before that wiring
    ///     landed (companion-pet-follow-rebroadcast finding).
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
