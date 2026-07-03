using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     The immutable, whole-world reference-data snapshot loaded once at GameServer boot (ADR-0011: the clean
///     equivalent of the legacy shared-memory .IMG/.BIN caches). Built exclusively by
///     <see cref="WorldDataCacheBuilder" />, published by <see cref="WorldDataLoader.InitializeAsync" /> before
///     the server accepts connections, and never mutated afterwards -- every zone actor and handler reads it
///     lock-free.
/// </summary>
public sealed class WorldDataCache
{
    /// <summary>Item templates by ItemId, bonus skills re-attached (world.Items + world.ItemBonusSkills).</summary>
    public required FrozenDictionary<int, ItemDefinition> ItemsById { get; init; }

    /// <summary>Skill templates by SkillId, with description lines and both grade rows (world.Skills + children).</summary>
    public required FrozenDictionary<int, SkillDefinition> SkillsById { get; init; }

    /// <summary>Monster templates by MonsterId, the 5 drop tables grouped in (world.Monsters + MonsterDrop*).</summary>
    public required FrozenDictionary<int, MonsterDefinition> MonstersById { get; init; }

    /// <summary>NPC templates by NpcId, the 5 child tables grouped in (world.Npcs + Npc*).</summary>
    public required FrozenDictionary<int, NpcDefinition> NpcsById { get; init; }

    /// <summary>Quest templates by QuestId, rewards and dialogue lines grouped in (world.Quests + children).</summary>
    public required FrozenDictionary<int, QuestDefinition> QuestsById { get; init; }

    /// <summary>Per-level progression rows by Level (world.Levels).</summary>
    public required FrozenDictionary<short, LevelRowDto> LevelsByLevel { get; init; }

    /// <summary>
    ///     Live zones by ZoneNumber, each carrying its (pre-filtered) portals, landing points, NPC placements
    ///     and monster spawn regions.
    /// </summary>
    public required FrozenDictionary<short, ZoneDefinition> ZonesByNumber { get; init; }

    /// <summary>Gem-socket definitions by GemSocketId (world.GemSockets).</summary>
    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsById { get; init; }

    /// <summary>Blood-exchange catalog in BloodExchangeSlot order (world.BloodExchangeCatalog, 3 real rows).</summary>
    public required ImmutableArray<BloodExchangeCatalogRowDto> BloodExchangeCatalog { get; init; }

    /// <summary>GM event definitions (world.EventDefinitions -- empty in the legacy dump, loaded for day-one parity).</summary>
    public required ImmutableArray<EventDefinitionRowDto> EventDefinitions { get; init; }

    /// <summary>Cash-shop products by product id, active AND inactive (world.ItemMallProducts).</summary>
    public required FrozenDictionary<int, ItemMallProductRowDto> ItemMallProductsById { get; init; }

    /// <summary>Reward-bundle slots grouped by RewardBundleId (world.RewardBundles + world.RewardBundleItems).</summary>
    public required FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> RewardBundleItemsByBundleId
    {
        get;
        init;
    }
}
