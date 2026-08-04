using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory;

public readonly record struct InventoryZoneCommand(
    int CharacterId,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource<ZoneCommandResult>? Applied = null,
    bool RecomputeCombatPoseAfterEquip = false,
    bool ClearEffectsAfterWeaponUnequip = false,
    int? InventoryDate = null,
    int? StoreDate = null,
    int? PetBagDate = null,
    GroundItemSpawnPlan? GroundItemSpawn = null,
    int? PetGrowth = null,
    byte? PetActivity = null,
    ImmutableArray<InventorySkillChange> SkillChanges = default,
    int? SkillPoints = null);

public readonly record struct InventoryContainerSnapshot(byte Container, ImmutableDictionary<byte, ItemStack> Slots);

public readonly record struct InventorySkillChange(byte Slot, LearnedSkill Skill);
