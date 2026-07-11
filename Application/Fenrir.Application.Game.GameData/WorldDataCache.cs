using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Immutable, whole-world reference-data snapshot loaded once at boot, never mutated afterwards -- every zone
///     actor and handler reads it lock-free.
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

    /// <summary>
    ///     Gem-socket effect-table rows keyed by (Type, Value02) instead of GemSocketId -- the lookup shape
    ///     <c>StatCalculator.SumGemSocketContribution</c> actually needs to fold a gem's contribution into a
    ///     derived stat (<c>GSOCKET::Search</c>, Server/ts25zone/GameSystem/GameSystem_08_Socket.cpp:22-32).
    ///     Built once by <see cref="WorldDataCacheBuilder" /> from the same validated <see cref="GemSocketsById" />
    ///     rows, first-GemSocketId-wins on a duplicate key (matching legacy's first-match linear scan; the loaded
    ///     data set enforces key uniqueness today, so this tiebreak is not currently exercised). The key MUST be
    ///     built with the same formula as
    ///     <see cref="Fenrir.Application.Game.Stats.StatCalculator.GemSocketTypeValueKey(byte, byte)" /> --
    ///     duplicated here rather than shared because <c>Fenrir.Application.Game.GameData</c> does not (and
    ///     should not) reference <c>Fenrir.Application.Game.Stats</c>; see
    ///     <see cref="WorldDataCacheBuilder" />'s own gem-socket-index remarks.
    /// </summary>
    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsByTypeAndValue { get; init; }

    /// <summary>
    ///     Blood-exchange catalog in BloodExchangeSlot order (world.BloodExchangeCatalog, 3 real rows), as of
    ///     this boot -- never mutated afterwards, per this type's own contract. A live reader (purchase
    ///     resolution, CZ_DEMAND_BLOOD_MARK_SEND) must instead read
    ///     <see cref="Domain.Commerce.CommerceCatalogCache.BloodExchangeCatalog" />, which is hot-reloaded
    ///     periodically; this field only still exists for <see cref="WorldDataLoader" />'s own boot-diagnostics
    ///     logging.
    /// </summary>
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

    /// <summary>
    ///     Computed once from <see cref="ItemMallProductsById" /> at boot -- see <see cref="CashCatalogBuilder" />.
    ///     Never mutated afterwards, per this type's own contract. A live reader (purchase resolution,
    ///     CZ_GET_CASH_ITEM_INFO_SEND) must instead read <see cref="Domain.Commerce.CommerceCatalogCache.CashCatalog" />,
    ///     which is hot-reloaded periodically.
    /// </summary>
    public required CashCatalogBuilder.CashCatalog CashCatalog { get; init; }

    /// <summary>
    ///     ZC_GET_CASH_ITEM_INFO_RECV.Version, as of this boot -- see <see cref="CashCatalog" />'s own remarks;
    ///     <see cref="Domain.Commerce.CommerceCatalogCache.CashCatalogVersion" /> is the live value.
    /// </summary>
    public required int CashCatalogVersion { get; init; }
}
