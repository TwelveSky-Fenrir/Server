using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Guards the still-inert portion of workstream B1's context seam: threading a populated
///     <see cref="CosmeticContext" />/<see cref="ZoneContext" />/<see cref="ConsumableContext" />/
///     <see cref="MountContext" /> through <see cref="StatCalculator" /> (or assembling one from a
///     <see cref="PlayerRuntimeState" /> in <see cref="EquipmentService.RecomputeStats" />) must produce the
///     exact same <see cref="EffectiveStats" /> as omitting it, for every input no formula reads yet.
///     <para>
///         Workstream B2 has since made the <see cref="ConsumableContext" />'s five <c>Eat*Potion</c> elixir
///         counters LIVE (life/mana/element fold into their derived stats via
///         <see cref="StatCalculator.ComputeBaseStats" />, gated on the zone's number), and workstream B5 then
///         made <see cref="CosmeticContext" />'s rune arrays LIVE (each socket's packed stat feeds the four base
///         attributes) -- so neither the elixir counters nor the rune arrays can any longer be part of a
///         "populated == default" guard. The elixir counters' live magnitude behavior is covered by
///         <see cref="ConsumableElixirStatFeedTests" /> and the rune arrays' by
///         <see cref="RuneStatContributionTests" />, not here.
///     </para>
///     <para>
///         Wave-6 (workstreams B6/B7/B8) carries forward the SAME carve-out for every field its own getter-wiring
///         manifest schedules for a getter call-site insert -- reconciled here ahead of that wiring landing so
///         the wiring pass itself never needs to touch this file: <see cref="CosmeticContext.CostumeNumber" />/
///         <see cref="CosmeticContext.CostumeValue" />/<see cref="CosmeticContext.CostumeEnchantCs" /> (B6
///         costume -- <see cref="CostumeContributionTests" />) and <see cref="CosmeticContext.StellarCoreNumber" />
///         (B6 stellar core -- <see cref="StellarCoreStatContributionTests" />); <see cref="ZoneContext.RankBuffType" />
///         (B7 rank-buff -- <c>RankBuffContributionTests</c>), <see cref="ZoneContext.TribeRole" /> (B7 tribe-role
///         -- <c>TribeRoleContributionTests</c>) and <see cref="ZoneContext.DrunkStateId" /> (B7 drunk/rage --
///         <c>DrunkRageContributionTests</c>); <see cref="MountContext.AnimalNumber" />/
///         <see cref="MountContext.AbsorbActive" />/<see cref="MountContext.AbsorbValue" /> (B8 mount Tier-2b
///         absorb -- <c>MountGradeContributionTests</c>). <see cref="ZoneContext.OrnamentInUse" /> and its two
///         time-remaining counters are deliberately NOT carved out despite B7 ornament also being wired: neither
///         <see cref="RichEquipment" /> nor <see cref="EquipmentContainer" /> equips decoration slots 9-12, so
///         <c>ResolveOrnamentTier</c> stays <c>NotActive</c> regardless -- if those fixtures ever gain deco
///         slots, the ornament fields must be carved out too (see <c>OrnamentContributionTests</c> for the live
///         magnitude coverage). This guard therefore asserts non-leakage only for what remains a pure,
///         still-unwired seam: <see cref="ZoneContext.RageGauge" />, <see cref="ZoneContext.GuildBuffActive" />/
///         <see cref="ZoneContext.GuildId" />, the consumable pill/potion-event flags, and the still-blocked
///         mount grade/runtime-attribute tiers.
///     </para>
/// </summary>
public class StatContextSignatureTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    // A context set with every field pushed off its neutral default -- if any getter secretly read one of
    // these, at least one stat would diverge from the default-context result.
    private static CosmeticContext PopulatedCosmetic()
    {
        // Rune arrays left at their neutral default (empty): workstream B5 made the rune->base-attribute feed
        // live (magnitude covered by RuneStatContributionTests), so a populated rune array would legitimately
        // change base Str/Dex/Vit/Ki and is no longer a valid "must equal default" input -- exactly the carve-out
        // B2 already applied to the Eat*Potion elixir counters.
        // CostumeNumber/CostumeValue/CostumeEnchantCs and StellarCoreNumber are ALSO left at their neutral
        // default: workstream B6 made the costume-value/enchant feed (Vitality/Strength/Ki/Wisdom/Critical/Luck)
        // and the stellar-core table lookups (attack/defense/crit-defense/elemental/max-life) live, so a
        // populated value here would legitimately change those stats -- see CostumeContributionTests and
        // StellarCoreStatContributionTests for the live magnitude coverage instead. CostumeState stays populated:
        // it is not read by any getter (see StatCalculator.CostumeContribution.cs remarks), so it remains a pure,
        // unconsumed seam field.
        return new CosmeticContext(
            default,
            default,
            CostumeState: 1);
    }

    private static ZoneContext PopulatedZone()
    {
        // RankBuffType/TribeRole/DrunkStateId are deliberately left at their neutral zero: workstreams B7
        // rank-buff, B7 tribe-role and B7 drunk/rage each made their respective getter-wiring insert land on
        // these exact fields (RankBuffContributionTests / TribeRoleContributionTests /
        // DrunkRageContributionTests cover the live magnitude), so a populated value here would legitimately
        // change MaxLife/AttackPower/DefensePower/Critical/etc. -- exactly the carve-out B2 already applied to
        // the Eat*Potion elixir counters. OrnamentInUse/Gold/Silver stay populated (see class remarks: no
        // fixture equips decoration slots 9-12, so the ornament gate never arms regardless). RageGauge and
        // GuildBuffActive/GuildId remain populated: still a pure, unconsumed seam.
        return new ZoneContext(
            ZoneNumber: 241,
            OrnamentInUse: true,
            OrnamentGoldTimeRemaining: 100,
            OrnamentSilverTimeRemaining: 50,
            RageGauge: 999,
            GuildBuffActive: true,
            GuildId: 42);
    }

    private static ConsumableContext PopulatedConsumable()
    {
        // The five Eat*Potion counters are deliberately left at their neutral zero: workstream B2 made them
        // live (see class remarks + ConsumableElixirStatFeedTests), so a non-zero counter would legitimately
        // change MaxLife/MaxMana/Element and is no longer a valid "must equal default" input. Only the
        // still-unwired pill/potion-event flags are pushed off default here, guarding that they don't leak.
        return new ConsumableContext(
            HpBoostActive: true,
            WarriorPillActive: true,
            MaxPotionEventNum: 20,
            EventTribe: 1);
    }

    private static MountContext PopulatedMount()
    {
        // AnimalNumber/AbsorbActive/AbsorbValue are deliberately left at their neutral default: workstream B8
        // made the Tier-2b absorb->primary-stat feed live (magnitude covered by MountGradeContributionTests), so
        // a populated value here would legitimately add AbsorbValue into Vitality/Strength/Ki/Wisdom -- exactly
        // the carve-out B2 already applied to the Eat*Potion elixir counters. AnimalGrade/RuntimeAttributes stay
        // populated: the Tier-1 grade-multiplier and Tier-2 flat-per-point passes remain blocked/unwired (see
        // B8-mount openQuestions), so both are still a pure, unconsumed seam.
        return new MountContext(
            AnimalGrade: 4,
            RuntimeAttributes: [10, 20, 30]);
    }

    private static CharacterBaseAttributes RichAttributes()
    {
        // Non-trivial values across every base stat the calculator reads, so many EffectiveStats fields are
        // non-zero and a leaked context read would show up somewhere.
        return new CharacterBaseAttributes(
            Vitality: 120, Strength: 90, Intelligence: 75, Dexterity: 60,
            Level: 100, Tribe: 0, PreviousTribe: 0, Title: 305, Halo: 40, RebirthCount: 8);
    }

    private static FrozenDictionary<short, LevelRowDto> RichLevels()
    {
        return new Dictionary<short, LevelRowDto>
        {
            [100] = new(100, 0, 100, 0, 250, 300, 40, 35, 20, 500, 400)
        }.ToFrozenDictionary();
    }

    private static ItemRowDto Item(
        int itemId, byte sort = 0, byte checkSetItem = 0,
        short strength = 0, short dexterity = 0, short vitality = 0, short intelligent = 0, short luck = 0,
        short attackPower = 0, short defensePower = 0, short attackSuccess = 0, short attackBlock = 0,
        short elementAttackPower = 0, short elementDefensePower = 0, byte critical = 0, byte capeInfo2 = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, checkSetItem, 0,
            strength, dexterity, vitality, intelligent, luck,
            attackPower, defensePower, attackSuccess, attackBlock,
            elementAttackPower, elementDefensePower, critical,
            0, 0, null,
            0, 0, 0, capeInfo2, 0);
    }

    // A spread of occupied slots that lights up primary stats, atk/def, hit/block, crit, luck and elementals.
    private static EquippedItemSlot[] RichEquipment()
    {
        return
        [
            new EquippedItemSlot(2, Item(90002, 1, vitality: 30, defensePower: 40, attackBlock: 5), 12, 6, 0, 0),
            new EquippedItemSlot(3, Item(90003, checkSetItem: 2, attackSuccess: 20, luck: 4), 8, 4, 0, 0),
            new EquippedItemSlot(7, Item(90007, 14, strength: 25, attackPower: 60, critical: 3), 10, 5, 0, 0),
            new EquippedItemSlot(4, Item(90004, intelligent: 15, elementAttackPower: 12, elementDefensePower: 9), 6, 3,
                0, 0)
        ];
    }

    private static BuffInfo RichBuffs()
    {
        var buffs = new BuffInfo { Buff = new int[70] };
        buffs.Buff[0] = 25; // +25% AttackPower
        buffs.Buff[2] = 10; // +10% AttackSuccess
        buffs.Buff[8] = 15; // +15% ElementAttackPower
        return buffs;
    }

    [Fact]
    public void ComputeBaseStats_PopulatedContexts_ProduceIdenticalResultToDefaults()
    {
        var attributes = RichAttributes();
        var equipment = RichEquipment();
        var levels = RichLevels();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var baseline = StatCalculator.ComputeBaseStats(attributes, equipment, levels, legacySetNumber: 5, pet: pet);

        var withContexts = StatCalculator.ComputeBaseStats(
            attributes, equipment, levels, legacySetNumber: 5, pet: pet,
            cosmetic: PopulatedCosmetic(), zone: PopulatedZone(),
            consumable: PopulatedConsumable(), mount: PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void ComputeEffectiveStats_PopulatedContexts_ProduceIdenticalResultToDefaults()
    {
        var attributes = RichAttributes();
        var equipment = RichEquipment();
        var levels = RichLevels();
        var buffs = RichBuffs();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var baseline = StatCalculator.ComputeEffectiveStats(attributes, equipment, levels, buffs,
            legacySetNumber: 5, pet: pet);

        var withContexts = StatCalculator.ComputeEffectiveStats(
            attributes, equipment, levels, buffs, legacySetNumber: 5, pet: pet,
            cosmetic: PopulatedCosmetic(), zone: PopulatedZone(),
            consumable: PopulatedConsumable(), mount: PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void ComputeEffectiveStats_NoEquipmentWithPopulatedContexts_StillMatchesDefaults()
    {
        // A second, minimal-input pass: with nothing equipped every stat is driven purely by base attributes,
        // so a leaked context read would be even easier to spot here.
        var attributes = RichAttributes();
        var levels = RichLevels();

        var baseline = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels);

        var withContexts = StatCalculator.ComputeEffectiveStats(
            attributes, NoEquipment, levels,
            cosmetic: PopulatedCosmetic(), zone: PopulatedZone(),
            consumable: PopulatedConsumable(), mount: PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void Contexts_DefaultInstances_AreNeutral()
    {
        var cosmetic = default(CosmeticContext);
        var zone = default(ZoneContext);
        var consumable = default(ConsumableContext);
        var mount = default(MountContext);

        Assert.True(cosmetic.RuneItemIds.IsDefaultOrEmpty);
        Assert.True(cosmetic.RuneStatValues.IsDefaultOrEmpty);
        Assert.Equal(0, cosmetic.CostumeNumber);
        Assert.Equal(0, cosmetic.StellarCoreNumber);

        Assert.Equal(0, zone.ZoneNumber);
        Assert.False(zone.OrnamentInUse);
        Assert.Equal(0, zone.RankBuffType);
        Assert.Equal(0, zone.RageGauge);

        Assert.Equal(0, consumable.EatLifePotion);
        Assert.False(consumable.HpBoostActive);
        Assert.False(consumable.WarriorPillActive);

        Assert.Equal(0, mount.AnimalNumber);
        Assert.False(mount.AbsorbActive);
        Assert.True(mount.RuntimeAttributes.IsDefaultOrEmpty);
    }

    // ---- EquipmentService.RecomputeStats assembly path ----

    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }

    private static WorldDataCache WorldData()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [90002] = new(Item(90002, 1, vitality: 30, defensePower: 40, attackBlock: 5),
                ImmutableArray<ItemBonusSkillRowDto>.Empty),
            [90007] = new(Item(90007, 14, strength: 25, attackPower: 60, critical: 3),
                ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();

        return new WorldDataCache
        {
            ItemsById = itemsById,
            SkillsById = EmptyFrozen<int, SkillDefinition>(),
            MonstersById = EmptyFrozen<int, MonsterDefinition>(),
            NpcsById = EmptyFrozen<int, NpcDefinition>(),
            QuestsById = EmptyFrozen<int, QuestDefinition>(),
            LevelsByLevel = RichLevels(),
            ZonesByNumber = EmptyFrozen<short, ZoneDefinition>(),
            GemSocketsById = EmptyFrozen<int, GemSocketRowDto>(),
            GemSocketsByTypeAndValue = EmptyFrozen<int, GemSocketRowDto>(),
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = EmptyFrozen<int, ItemMallProductRowDto>(),
            RewardBundleItemsByBundleId = EmptyFrozen<int, ImmutableArray<RewardBundleItemRowDto>>(),
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static ImmutableDictionary<byte, ItemStack> EquipmentContainer()
    {
        return ImmutableDictionary<byte, ItemStack>.Empty
            .Add(2, new ItemStack(90002, 1, 12, 6, 0, 0, 0, 0, 0, 0, 0))
            .Add(7, new ItemStack(90007, 1, 10, 5, 0, 0, 0, 0, 0, 0, 0));
    }

    // A runtime state with every still-inert context-sourcing field pushed off its neutral default (the five
    // now-live Eat*Potion counters excepted -- see the assignment note below).
    private static PlayerRuntimeState PopulatedState()
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 100,
            MapId = 241,
            // RuneSystem/RuneSystemStat left at their default (all-zero, empty sockets): B5 made the rune feed
            // live, so a populated rune array would legitimately change base stats here and break the
            // populated-state == null-state invariant this test asserts. Its magnitude behavior lives in
            // RuneStatContributionTests instead -- same carve-out reason as PopulatedCosmetic's rune arrays.
            // CostumeNumber/StellarCoreNumber left at their zero default on purpose -- B6 made the
            // costume-value/enchant feed and the stellar-core table lookups live via this exact assembly path
            // (AssembleStatContexts resolves CostumeValue from worldData.ItemsById), so a non-zero id here would
            // legitimately change base stats and break the populated-state == null-state invariant. CostumeState
            // stays populated (not read by any getter -- pure seam).
            CostumeState = 1,
            UseOrnament = true,
            // RankBuffType/TribeRole left at their zero default on purpose -- B7 rank-buff/tribe-role made these
            // exact fields live (RankBuffContributionTests / TribeRoleContributionTests cover the magnitude), so
            // a non-zero value here would legitimately change MaxLife/AttackPower/DefensePower/etc.
            GuildId = 42,
            GuildBuffActive = true,
            // Eat*Potion counters left at their zero default on purpose -- B2 made them live, so they belong to
            // ConsumableElixirStatFeedTests, not this non-leakage guard (see class remarks).
            // AnimalNumber/AnimalAbsorbState stay populated: B8's Tier-2b absorb feed reads MountContext.AbsorbValue,
            // which AssembleStatContexts leaves at 0 (no animal-catalog lookup exists yet), so this cannot leak
            // regardless -- unlike CostumeNumber/RankBuffType/TribeRole above, no carve-out is needed here.
            AnimalNumber = 1234,
            AnimalAbsorbState = 1,
            MountRolledAttributes = [10, 20, 30]
        };
    }

    [Fact]
    public void RecomputeStats_WithPopulatedRuntimeState_MatchesNullRuntimeState()
    {
        var attributes = RichAttributes();
        var worldData = WorldData();
        var equipment = EquipmentContainer();
        var buffs = RichBuffs();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData, buffs, pet);
        var withState = EquipmentService.RecomputeStats(attributes, equipment, worldData, buffs, pet, PopulatedState());

        Assert.Equal(withoutState, withState);
    }
}
