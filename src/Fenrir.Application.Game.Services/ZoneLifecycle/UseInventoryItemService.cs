using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Domain.Game.Stats.Context;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class UseInventoryItemService(
    ICharacterRepository characters,
    IGuildRepository guilds,
    ICashRepository cash,
    IOfflineShopRepository offlineShops,
    IEventLogRepository eventLog,
    IProxyShopExpirationRelayQueue proxyShopExpirationRelay,
    IOptions<GameServerOptions> options,
    WorldDataCache worldData,
    ILogger<UseInventoryItemService> logger,
    ITowerUpgradeService towerUpgrade,
    IWorldNoticeService worldNotice,
    ZoneRegistry zones,
    UseItemHandlerRegistry? useItemRegistry = null) : IUseInventoryItemService
{
    private const byte BottleSort = 26;

    private const byte MixSkillSort = 23;

    private const byte MixSkillSort2 = 24;

    private const int DeathProtectionScrollAmount = 20;

    private const int MountExpAlreadyMaxedResult = 2;

    private const int GuildScrollRejectedResult = 3;

    private const int GuildScrollNoGuildResult = 4;

    private const int RebirthResetZone101TimeRefund = 10000;

    private const int RebirthResetElixirCap = 200;

    private const int TowerConstructItemId = 665;

    private const int TowerHealItemId = 667;

    private const byte SkillGrimoireSort = 5;

    private const short SkillGrimoireItemConsumedEventCode = 2;

    private const short SkillGrimoireSkillLearnedEventCode = 3;

    private const byte SkillGrimoireLearnSuccessOutcome = 1;

    private const byte GpTicketCashCreditReason = 2;

    private const short GpTicketRedeemedEventCode = 1;

    private const short ProxyShopRentalExtensionEventCode = 2;

    private const short PetExpBoostPillUsedEventCode = 3;

    private const short PetFoodUsedEventCode = 4;

    private const int LodTicketItemId = 1434;
    private const int FactionNoticeItemId = 566;
    private const int TaiyanKeyItemId = 1049;
    private const int BookOfAmnesiaItemId = 1027;
    private const int ReductionSutraItemId = 1066;
    private const int RebirthResetScrollItemId = 886;
    private const int FortunePouchItemId = 1045;

    private const int CpRandomBagItemId = 99101;

    private const int AppearanceChangeScrollItemId = 1214;

    private const int GenderScrollItemId = 1171;

    private const int PremiumService1DayA = 2292;
    private const int PremiumService1DayB = 8420;
    private const int PremiumService1DayC = 8001;
    private const int PremiumService3Days = 8002;
    private const int PremiumService7DaysA = 8421;
    private const int PremiumService7DaysB = 8003;
    private const int PremiumService30Days = 2138;

    private const int MoneySilverDeltaSort = 23;

    private const short TeleportRecallScrollUsedEventCode = 1;

    private const byte TeleportRecallScrollSuccessOutcome = 1;

    private const short StatPotionUsedEventCode = 4;

    private const byte StatPotionSuccessOutcome = 1;

    private const int SilverScrollItemId = 1370;

    private const int GoldScrollItemId = 1167;

    private const int WarPointBoxItemId = 8110;

    private const int WarPointBoxGrantAmount = 5;

    private const int BuffDurationPillItemId = 1132;

    private const int BuffX2TimeStatSort = 42;

    private const int SilverScrollDurationMinutes = 180;

    private const int GoldScrollDurationMinutes = 240;


    private const int DoubleKillNumTimeSortCode = 4;

    private const int DoubleKillExpTimeSortCode = 5;

    private const int DoubleKillNumTime2SortCode = 30;

    private const int DoubleKillNumTime2ChargeAmount = 50;

    private const int AutoHuntBuffStoreLength = 16;

    private const int AutoHuntAttackTypeLength = 4;

    private const int PetInventoryRenewalItemId = 829;

    private const int PetInventoryRenewalDays = 30;

    private static readonly ImmutableHashSet<int> TribeConversionBookItemIds =
        ImmutableHashSet.Create(99014, 99015, 99016);

    private static readonly ImmutableHashSet<int> RebirthOnlyStatBoostItemIds =
        ImmutableHashSet.Create(1191, 1192, 1193, 1194, 1195, 1196, 1197, 1198, 1199, 626, 627, 628, 17037);

    public async ValueTask<UseInventoryItemResponse?> ResolveAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, int value, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
            return Fail(characterId, itemStack, page, index, value);

        if (itemDefinition.Item.Sort is MixSkillSort or MixSkillSort2 || item.ItemId is 8150 or 8151 or 8152)
            return Fail(characterId, item, page, index, value);

        if (itemDefinition.Item.Sort == BottleSort)
            return await ResolveBottleAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (itemDefinition.Item.Sort == SkillGrimoireSort && !TribeConversionBookItemIds.Contains(item.ItemId))
            return await ResolveSkillGrimoireAsync(zone, state, characterId, accountId, page, index, item,
                itemDefinition, cancellationToken);

        if (GpTicketCatalog.ResolveCreditAmount(item.ItemId) is { } creditAmount)
            return await ResolveGpTicketAsync(zone, state, characterId, accountId, page, index, item, creditAmount,
                cancellationToken);

        if (item.ItemId == LodTicketItemId)
            return await ResolveLodTicketAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (ResolveStatsClearBand(item.ItemId) is { } clearBand)
            return await ResolveStatsClearAsync(zone, state, characterId, page, index, item, clearBand,
                cancellationToken);

        if (ResolveStatCleanseBand(item.ItemId) is { } cleanseBand)
            return await ResolveStatCleanseAsync(zone, state, characterId, page, index, item, cleanseBand, value,
                cancellationToken);

        if (ResolveCharmFamily(item.ItemId) is { } charmSpec)
            return await ResolveProtectionCharmAsync(zone, state, characterId, page, index, item, value,
                charmSpec.Kind, charmSpec.PerUnitAmount, cancellationToken);

        if (ResolveScrollFamily(item.ItemId) is { } scrollSpec)
            return await ResolveProtectionScrollAsync(zone, state, characterId, page, index, item, scrollSpec.Kind,
                scrollSpec.FixedAmount, cancellationToken);

        if (IsDeathProtectionScroll(item.ItemId))
            return await ResolveDeathProtectionScrollAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        if (item.ItemId == FactionNoticeItemId)
            return await ResolveFactionNoticeAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (item.ItemId == TaiyanKeyItemId)
            return await ResolveTaiyanKeyAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (GuildScrollBuffMinutes(item.ItemId) is { } minutes)
            return await ResolveGuildScrollAsync(zone, state, characterId, page, index, item, minutes,
                cancellationToken);


        if (ProxyShopRentalExtensionResolver.ExtensionDaysFor(item.ItemId) is not null)
            return await ResolveProxyShopRentalExtensionAsync(zone, state, characterId, page, index, item,
                itemDefinition.Item.Sort, cancellationToken);

        if (IsTeleportRecallScroll(item.ItemId))
            return await ResolveTeleportRecallScrollAsync(characterId, accountId, page, index, item, value,
                cancellationToken);

        if (ResolveStatPotionSpec(item.ItemId) is { } statPotionSpec)
            return await ResolveStatPotionAsync(zone, state, characterId, accountId, page, index, item,
                statPotionSpec.Kind, statPotionSpec.Tier, value, cancellationToken);

        if (IsPetExpBoostPill(item.ItemId))
            return await ResolvePetExpBoostPillAsync(zone, state, characterId, accountId, page, index, item, value,
                cancellationToken);

        if (PetFoodFeedResolver.IsPetFood(item.ItemId))
            return await ResolvePetFoodAsync(zone, state, characterId, accountId, page, index, item, value,
                cancellationToken);

        if (IsRebirthPill(item.ItemId))
            return await ResolveRebirthPillAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (item.ItemId == TowerConstructItemId)
            return await towerUpgrade.ConstructAsync(characterId, zone, state, page, index, item, value,
                cancellationToken);

        if (item.ItemId == TowerHealItemId)
            return await towerUpgrade.HealAsync(characterId, zone, state, page, index, item, cancellationToken);

        if (item.ItemId == BookOfAmnesiaItemId)
            return await ResolveBookOfAmnesiaAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (item.ItemId == ReductionSutraItemId)
            return await ResolveReductionSutraAsync(zone, state, characterId, page, index, item, value,
                cancellationToken);

        if (item.ItemId == RebirthResetScrollItemId)
            return await ResolveRebirthResetScrollAsync(zone, state, characterId, page, index, item, cancellationToken);
        if (MountExpScrollAmount(item.ItemId) is { } mountExpAmount)
            return await ResolveMountExpScrollAsync(zone, state, characterId, page, index, item, mountExpAmount,
                item.ItemId is 17040 or 17041, cancellationToken);

        if (ResolveDoubleExpTime1Amount(item.ItemId) is { } dexp1Amount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dexp1Amount, 17, static s => s.DoubleExpTime1,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleExpTime1: v),
                cancellationToken);

        if (ResolveDoubleExpTime2Amount(item.ItemId) is { } dexp2Amount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dexp2Amount, 43, static s => s.DoubleExpTime2,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleExpTime2: v),
                cancellationToken);

        if (ResolveFightingGodForDestroyAmount(item.ItemId) is { } fgfdAmount)
        {
            if (state.Level is < 1 or > 112)
                return Fail(characterId, item, page, index);
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                fgfdAmount, 20, static s => s.FightingGodForDestroy,
                static (id, v) => new TribeProgressZoneCommand(id, FightingGodForDestroy: v),
                cancellationToken);
        }

        if (RebirthOnlyStatBoostItemIds.Contains(item.ItemId))
        {
            logger.LogInformation(
                "Character {CharacterId} use-inventory-item (stat-boost scroll {ItemId}): dead feature -- WUSE_ITEM_1191/WUSE_ITEM_626 are guarded by __REBIRTH__ (Server/Header/use_inventory.h:104-107), which has its only #define inside the dead #else of #ifdef M33 (Server/Header/Protocol/DEFINE.h:28); the legacy drops the session here, so the four counters are permanently 0 -- item kept",
                characterId, item.ItemId);
            return Fail(characterId, item, page, index);
        }

        if (item.ItemId is 1227 or 8439)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                30, 87, static s => s.WarriorScroll,
                static (id, v) => new TribeProgressZoneCommand(id, WarriorScroll: v),
                cancellationToken);

        if (item.ItemId == CpRandomBagItemId)
            return await ResolveCpRandomBagAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (item.ItemId == FortunePouchItemId)
            return await ResolveFortunePouchAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (ResolvePremiumDays(item.ItemId) is { } premiumDays)
            return await ResolvePremiumServiceAsync(zone, state, characterId, page, index, item, premiumDays,
                cancellationToken);

        if (ResolveAutoBuffScrollDays(item.ItemId) is { } buffDays)
            return await ResolveAutoBuffScrollAsync(zone, state, characterId, page, index, item, buffDays,
                cancellationToken);

        if (ResolveMountAbsorbMinutes(item.ItemId) is { } absorbMinutes)
            return await ResolveMountAbsorbScrollAsync(zone, state, characterId, page, index, item, absorbMinutes,
                cancellationToken);

        if (ResolveMountDoubleExpMinutes(item.ItemId) is { } doubleExpMinutes)
            return await ResolveMountDoubleExpScrollAsync(zone, state, characterId, page, index, item, doubleExpMinutes,
                cancellationToken);

        if (ResolveAutoHuntMinutesAmount(item.ItemId) is { } ahMinutes)
            return await ResolveAutoHuntMinutesScrollAsync(zone, state, characterId, page, index, item, ahMinutes,
                cancellationToken);

        if (ResolveAutoHuntDaysAmount(item.ItemId) is { } ahDays)
            return await ResolveAutoHuntDaysScrollAsync(zone, state, characterId, page, index, item, ahDays,
                cancellationToken);

        if (item.ItemId == AppearanceChangeScrollItemId)
            return await ResolveAppearanceChangeScrollAsync(zone, state, characterId, page, index, item, value,
                cancellationToken);

        if (item.ItemId == GenderScrollItemId)
            return await ResolveGenderScrollAsync(zone, state, characterId, page, index, item, value,
                cancellationToken);

        if (InstantExpPillFormulas.IsInstantExpPill(item.ItemId))
            return await ResolveInstantExpPillAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (item.ItemId == SilverScrollItemId)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                SilverScrollDurationMinutes, 90, static s => s.SilverTime,
                static (id, v) => new TribeProgressZoneCommand(id, SilverTime: v),
                cancellationToken);

        if (item.ItemId == GoldScrollItemId)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                GoldScrollDurationMinutes, 101, static s => s.GoldTime,
                static (id, v) => new TribeProgressZoneCommand(id, GoldTime: v),
                cancellationToken);

        if (ResolveDoubleKillBothAmount(item.ItemId) is { } dkBothAmount)
            return await ResolveDoubleKillBothScrollAsync(zone, state, characterId, page, index, item, dkBothAmount,
                cancellationToken);

        if (ResolveDoubleKillNumTimeAmount(item.ItemId) is { } dknAmount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dknAmount, DoubleKillNumTimeSortCode, static s => s.DoubleKillNumTime,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleKillNumTime: v),
                cancellationToken);

        if (ResolveDoubleKillExpTimeAmount(item.ItemId) is { } dkeAmount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dkeAmount, DoubleKillExpTimeSortCode, static s => s.DoubleKillExpTime,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleKillExpTime: v),
                cancellationToken);

        if (item.ItemId is 1155 or 8438)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                DoubleKillNumTime2ChargeAmount, DoubleKillNumTime2SortCode, static s => s.DoubleKillNumTime2,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleKillNumTime2: v),
                cancellationToken);

        if (item.ItemId == BuffDurationPillItemId)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                60, BuffX2TimeStatSort, static s => s.BuffX2Time,
                static (id, v) => new TribeProgressZoneCommand(id, BuffX2Time: v),
                cancellationToken);

        if (item.ItemId is 979 or 980 or 981)
        {
            logger.LogInformation(
                "Character {CharacterId} use-inventory-item (Stats Convert Scroll {ItemId}): dead feature — item consumed",
                characterId, item.ItemId);
            return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
        }

        if (item.ItemId == WarPointBoxItemId)
            return await ResolveWarPointBoxAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (ResolveHermitsVaultRenewalDays(item.ItemId) is { } hermitsVaultDays)
            return await ResolveVaultRenewalAsync(zone, state, characterId, page, index, item, hermitsVaultDays,
                VaultRenewalTarget.HermitsVault, cancellationToken);

        if (ResolveStorageVaultRenewalDays(item.ItemId) is { } storageVaultDays)
            return await ResolveVaultRenewalAsync(zone, state, characterId, page, index, item, storageVaultDays,
                VaultRenewalTarget.StorageVault, cancellationToken);

        if (item.ItemId == PetInventoryRenewalItemId)
            return await ResolveVaultRenewalAsync(zone, state, characterId, page, index, item,
                PetInventoryRenewalDays, VaultRenewalTarget.PetInventory, cancellationToken);

        if (useItemRegistry?.Resolve(item, itemDefinition) is { } useItemHandler)
            return await useItemHandler.HandleAsync(
                new UseItemContext(zone, state, characterId, accountId, page, index, item, itemDefinition, value,
                    session),
                cancellationToken);

        if (itemDefinition.Item.Sort == HotkeyItemConsumptionResolver.ConsumableItemCategory
            && itemDefinition.Item.PotionType1 is >= 1 and <= 5)
            return await ResolveConsumablePotionAsync(zone, state, characterId, page, index, item,
                itemDefinition.Item.PotionType1, itemDefinition.Item.PotionType2, cancellationToken);

        return Unrecognized(state, characterId, accountId, item, page, index, value);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveConsumablePotionAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        int potionType1, int potionType2, CancellationToken cancellationToken)
    {
        if (state.IsStunned || state.IsDead)
            return Fail(characterId, item, page, index);

        if (!state.CanUseConsumables)
            return Fail(characterId, item, page, index);

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

        int? lifeGain = null;
        int? manaGain = null;

        switch (potionType1)
        {
            case 1:
            {
                if (state.Life >= maxLife)
                    return Fail(characterId, item, page, index);
                lifeGain = PotionGain(false, potionType2, maxLife, state.Life);
                break;
            }
            case 2:
            {
                if (state.Life >= maxLife)
                    return Fail(characterId, item, page, index);
                lifeGain = PotionGain(true, potionType2, maxLife, state.Life);
                break;
            }
            case 3:
            {
                if (state.Mana >= maxMana)
                    return Fail(characterId, item, page, index);
                manaGain = PotionGain(false, potionType2, maxMana, state.Mana);
                break;
            }
            case 4:
            {
                if (state.Mana >= maxMana)
                    return Fail(characterId, item, page, index);
                manaGain = PotionGain(true, potionType2, maxMana, state.Mana);
                break;
            }
            case 5:
            {
                if (state.Life >= maxLife && state.Mana >= maxMana)
                    return Fail(characterId, item, page, index);
                lifeGain = PotionGain(true, potionType2, maxLife, state.Life);
                manaGain = PotionGain(true, potionType2, maxMana, state.Mana);
                break;
            }
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, LifeGain: lifeGain, ManaGain: manaGain),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped inventory-potion mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (consumable potion) applied: item {ItemId} potionType1={PotionType1} life+{LifeGain} mana+{ManaGain}",
            characterId, item.ItemId, potionType1, lifeGain ?? 0, manaGain ?? 0);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int PotionGain(bool isPercent, int potionType2, int effectiveMax, int current)
    {
        var raw = isPercent ? effectiveMax * potionType2 / 100 : potionType2;
        return Math.Clamp(raw, 0, Math.Max(0, effectiveMax - current));
    }

    private UseInventoryItemResponse Unrecognized(PlayerRuntimeState state, int characterId, int accountId,
        ItemStack item, byte page, byte index, int value)
    {
        logger.LogWarning(
            "Character {CharacterId} ({CharacterName}, account {AccountId}, shard {ShardId}) use-inventory-item unrecognized: item {ItemId} matched no recognized dispatch branch",
            characterId, state.Name, accountId, options.Value.ShardId, item.ItemId);
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = value, Value2 = 0 };
    }

    private static short StatPotionSubTypeEventCode(StatPotionKind kind, StatPotionTier tier)
    {
        var isG12 = tier == StatPotionTier.G12;
        return kind switch
        {
            StatPotionKind.Life => isG12 ? (short)11 : (short)10,
            StatPotionKind.Mana => isG12 ? (short)13 : (short)12,
            StatPotionKind.Str => isG12 ? (short)15 : (short)14,
            StatPotionKind.Dex => isG12 ? (short)17 : (short)16,
            StatPotionKind.ElementalDamage => isG12 ? (short)19 : (short)18,
            StatPotionKind.ElementalDefense => isG12 ? (short)21 : (short)20,
            _ => 0
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveBottleAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = BottleResolver.ResolveAcquire(state.BottleSlots, item.ItemId);
        if (resolved.Outcome == BottleResolver.AcquireOutcome.Rejected)
            return Fail(characterId, item, page, index);

        var projected = state.Inventory.GetContainer(page).Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = resolved.SlotIndex, Value2 = 0
        };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item (bottle) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, resolved.SlotIndex,
                resolved.RefilledCount, state.Life, item.ItemId)))
            logger.LogError(
                "Zone {MapId} bottle inbox full: dropped bottle-acquire mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (bottle) applied: item {ItemId} acquired bottle slot {SlotIndex}",
            characterId, item.ItemId, resolved.SlotIndex);

        return response;
    }

    private async ValueTask<UseInventoryItemResponse> ResolveSkillGrimoireAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, ItemStack item, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        var skillDefinition = itemDefinition.Item.GainSkillNumber is { } grantedSkillId
            ? worldData.SkillsById.GetValueOrDefault(grantedSkillId)
            : null;

        var resolved = SkillGrimoireLearnResolver.Resolve(itemDefinition.Item.EquipInfo1,
            itemDefinition.Item.LevelLimit, itemDefinition.Item.MartialLevelLimit,
            itemDefinition.Item.GainSkillNumber, state.PreviousTribe, state.Level + state.Level2, skillDefinition,
            state.LearnedSkills, state.SkillPoints);

        if (resolved.Outcome != SkillGrimoireLearnResolver.Outcome.Success)
            return Fail(characterId, item, page, index);

        var newSkillPoints = state.SkillPoints - resolved.Cost;
        var learned = new LearnedSkill(resolved.SkillId, resolved.Cost);

        await characters.UpsertSkillSlotAsync(characterId, resolved.Slot, resolved.SkillId, resolved.Cost,
            cancellationToken);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, resolved.Slot, learned, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped skill-grimoire learn mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        await eventLog.LogAsync(SkillGrimoireItemConsumedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, item.Quantity, SkillGrimoireLearnSuccessOutcome,
            $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

        await eventLog.LogAsync(SkillGrimoireSkillLearnedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, null, SkillGrimoireLearnSuccessOutcome,
            $"Slot={resolved.Slot};SkillId={resolved.SkillId};Cost={resolved.Cost};SkillPoints={newSkillPoints}",
            cancellationToken);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveGpTicketAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, ItemStack item, int creditAmount,
        CancellationToken cancellationToken)
    {
        var projected = state.Inventory.GetContainer(page).Remove(index);

        try
        {
            await cash.CreditAndConsumeItemAsync(accountId, creditAmount, GpTicketCashCreditReason, item.ItemId,
                characterId, page, ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} GP ticket credit-and-consume failed for item {ItemId} (character {CharacterId}); no cash credited and item left untouched",
                accountId, item.ItemId, characterId);
            return Fail(characterId, item, page, index);
        }

        await eventLog.LogAsync(GpTicketRedeemedEventCode, EventLogCategory.Currency, accountId, characterId,
            null, null, null, creditAmount, null, item.ItemId, item.Quantity, 1, null, cancellationToken);

        var response = new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item (GP ticket) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Account {AccountId} use-inventory-item (GP ticket) applied: item {ItemId} credited {CreditAmount} to character {CharacterId}",
            accountId, item.ItemId, creditAmount, characterId);

        return response;
    }

    private async ValueTask<UseInventoryItemResponse> ResolveGuildScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        if (state.GuildId is not { } guildId)
        {
            logger.LogDebug(
                "Character {CharacterId} guild scroll rejected: not in a guild (item {ItemId})",
                characterId, item.ItemId);
            return new UseInventoryItemResponse
                { Result = GuildScrollNoGuildResult, Page = page, Index = index, Value = 0, Value2 = 0 };
        }

        var guild = await guilds.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            logger.LogWarning(
                "Character {CharacterId} guild scroll rejected: guild {GuildId} not found (item {ItemId})",
                characterId, guildId, item.ItemId);
            return new UseInventoryItemResponse
                { Result = GuildScrollRejectedResult, Page = page, Index = index, Value = 0, Value2 = 0 };
        }

        GuildBuffTopUp.Result topUp;
        try
        {
            topUp = GuildBuffTopUp.Apply(guild, minutes, DateTimeOffset.UtcNow);
            await guilds.SetBuffAsync(guildId, topUp.BuffType, topUp.BuffState, topUp.BuffTime,
                topUp.BuffTimeForDiff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild scroll recharge failed for guild {GuildId}", characterId, guildId);
            return Fail(characterId, item, page, index);
        }

        var activation = new GuildBuffActivationZoneCommand(guildId, topUp.BuffType, topUp.BuffState != 0);
        foreach (var memberZone in zones.Zones)
            memberZone.PostGuildBuffActivationCommand(in activation);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveProxyShopRentalExtensionAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, byte itemSort,
        CancellationToken cancellationToken)
    {
        var today = GameDate.Today();
        var (shop, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        var currentExpiration = shop?.ShopDate ?? 0;

        var resolved = ProxyShopRentalExtensionResolver.Resolve(item.ItemId, today, currentExpiration);
        if (resolved.Outcome != ProxyShopRentalExtensionResolver.Outcome.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} use-inventory-item (proxy-shop rental extension) rejected by resolver (item {ItemId}, outcome {Outcome})",
                characterId, item.ItemId, resolved.Outcome);
            return new UseInventoryItemResponse
                { Result = 1, Page = page, Index = index, Value = resolved.NewExpirationDate, Value2 = 0 };
        }

        try
        {
            await offlineShops.ExtendRentalAsync(characterId, resolved.NewExpirationDate, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} proxy-shop rental-extension ExtendRentalAsync failed", characterId);
            return new UseInventoryItemResponse
                { Result = 1, Page = page, Index = index, Value = resolved.NewExpirationDate, Value2 = 0 };
        }

        await eventLog.LogAsync(ProxyShopRentalExtensionEventCode, EventLogCategory.CashItemUse, null, characterId,
            null, null, null, null, null, item.ItemId, item.Quantity, 0,
            $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

        var remaining = CashItemStackConsumption.RemainingQuantity(itemSort, item.Quantity);
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse
            { Result = 0, Page = page, Index = index, Value = resolved.NewExpirationDate, Value2 = 0 };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped proxy-shop rental-extension mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        zone.TryUpdateProxyShopExpiration(characterId, resolved.NewExpirationDate);

        proxyShopExpirationRelay.Enqueue(
            new ProxyShopExpirationRelayEntry(options.Value.ShardId, characterId, resolved.NewExpirationDate));

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (proxy-shop rental extension) applied: item {ItemId}, new expiration {NewExpirationDate}",
            characterId, item.ItemId, resolved.NewExpirationDate);

        return response;
    }

    private async ValueTask<UseInventoryItemResponse> ResolveTeleportRecallScrollAsync(int characterId,
        int accountId, byte page, byte index, ItemStack item, int value, CancellationToken cancellationToken)
    {
        if (item.Quantity < 1)
            return Fail(characterId, item, page, index);

        await eventLog.LogAsync(TeleportRecallScrollUsedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, item.Quantity, TeleportRecallScrollSuccessOutcome,
            $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (teleport/recall scroll) applied: item {ItemId} (no inventory mutation)",
            characterId, item.ItemId);

        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = value, Value2 = 0 };
    }

    private static bool IsTeleportRecallScroll(int itemId)
    {
        return itemId is 1109 or 1224 or 1026;
    }

    private static StatPotionSpec? ResolveStatPotionSpec(int itemId)
    {
        return itemId switch
        {
            506 or 1017 or 17038 => new StatPotionSpec(StatPotionKind.Life, StatPotionTier.Single),
            636 or 17026 => new StatPotionSpec(StatPotionKind.Life, StatPotionTier.TenStack),
            801 => new StatPotionSpec(StatPotionKind.Life, StatPotionTier.G12),

            507 or 1018 or 17039 => new StatPotionSpec(StatPotionKind.Mana, StatPotionTier.Single),
            637 or 17027 => new StatPotionSpec(StatPotionKind.Mana, StatPotionTier.TenStack),
            802 => new StatPotionSpec(StatPotionKind.Mana, StatPotionTier.G12),

            509 or 1092 => new StatPotionSpec(StatPotionKind.Str, StatPotionTier.Single),
            638 or 17028 => new StatPotionSpec(StatPotionKind.Str, StatPotionTier.TenStack),
            803 => new StatPotionSpec(StatPotionKind.Str, StatPotionTier.G12),

            508 or 1093 => new StatPotionSpec(StatPotionKind.Dex, StatPotionTier.Single),
            639 or 17029 => new StatPotionSpec(StatPotionKind.Dex, StatPotionTier.TenStack),
            804 => new StatPotionSpec(StatPotionKind.Dex, StatPotionTier.G12),

            578 => new StatPotionSpec(StatPotionKind.ElementalDamage, StatPotionTier.Single),
            640 => new StatPotionSpec(StatPotionKind.ElementalDamage, StatPotionTier.TenStack),
            805 => new StatPotionSpec(StatPotionKind.ElementalDamage, StatPotionTier.G12),

            579 => new StatPotionSpec(StatPotionKind.ElementalDefense, StatPotionTier.Single),
            641 => new StatPotionSpec(StatPotionKind.ElementalDefense, StatPotionTier.TenStack),
            806 => new StatPotionSpec(StatPotionKind.ElementalDefense, StatPotionTier.G12),

            _ => null
        };
    }

    private static int CurrentStatPotionSubValue(PlayerRuntimeState state, StatPotionKind kind)
    {
        return kind switch
        {
            StatPotionKind.Life => state.EatLifePotion,
            StatPotionKind.Mana => state.EatManaPotion,
            StatPotionKind.Str => state.EatStrPotion,
            StatPotionKind.Dex => state.EatDexPotion,
            StatPotionKind.ElementalDamage => ElementalPotionPacking.DamageSubValue(state.EatElePotion),
            StatPotionKind.ElementalDefense => ElementalPotionPacking.DefenseSubValue(state.EatElePotion),
            _ => 0
        };
    }

    private static int ResolvedStatPotionRawCounter(PlayerRuntimeState state, StatPotionKind kind, int newSubValue)
    {
        return kind switch
        {
            StatPotionKind.ElementalDamage =>
                ElementalPotionPacking.WithDamageSubValue(state.EatElePotion, newSubValue),
            StatPotionKind.ElementalDefense => ElementalPotionPacking.WithDefenseSubValue(state.EatElePotion,
                newSubValue),
            _ => newSubValue
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveStatPotionAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, ItemStack item, StatPotionKind kind,
        StatPotionTier tier, int requestedValue, CancellationToken cancellationToken)
    {
        var bulkRequested = BulkUseCoercion.Coerce(requestedValue, item.Quantity);
        var currentSubValue = CurrentStatPotionSubValue(state, kind);

        int newSubValue;
        int unitsConsumed;

        if (tier == StatPotionTier.G12)
        {
            var g12 = StatPotionResolver.ResolveG12(currentSubValue, state.Level2, bulkRequested);
            if (!g12.Succeeded)
                return Fail(characterId, item, page, index);

            newSubValue = g12.NewCount;
            unitsConsumed = g12.UnitsConsumed;
        }
        else
        {
            var perUnitAmount = tier == StatPotionTier.TenStack ? 10 : 1;
            var ordinary = StatPotionResolver.ResolveOrdinary(currentSubValue, perUnitAmount, bulkRequested);
            if (!ordinary.Succeeded)
                return Fail(characterId, item, page, index);

            newSubValue = ordinary.NewCount;
            unitsConsumed = ordinary.UnitsConsumed;
        }

        var newRawCounter = ResolvedStatPotionRawCounter(state, kind, newSubValue);

        var baseConsumable = new ConsumableContext(state.EatLifePotion, state.EatManaPotion, state.EatStrPotion,
            state.EatDexPotion, state.EatElePotion,
            state.HPBoost > 0,
            state.WarriorPill > 0,
            state.DmgBoost > 0,
            state.CriBoost > 0);
        var consumableOverride = kind switch
        {
            StatPotionKind.Life => baseConsumable with { EatLifePotion = newRawCounter },
            StatPotionKind.Mana => baseConsumable with { EatManaPotion = newRawCounter },
            StatPotionKind.Str => baseConsumable with { EatStrPotion = newRawCounter },
            StatPotionKind.Dex => baseConsumable with { EatDexPotion = newRawCounter },
            _ => baseConsumable with { EatElePotion = newRawCounter }
        };

        var updatedStats = RecomputeStatsAfterReset(state, state.StatVit, state.StatStr, state.StatInt,
            state.StatDex, consumableOverride);

        var command = kind switch
        {
            StatPotionKind.Life => new TribeProgressZoneCommand(characterId, EatLifePotion: newRawCounter,
                UpdatedStats: updatedStats, FullActionRebroadcast: true),
            StatPotionKind.Mana => new TribeProgressZoneCommand(characterId, EatManaPotion: newRawCounter,
                UpdatedStats: updatedStats, FullActionRebroadcast: true),
            StatPotionKind.Str => new TribeProgressZoneCommand(characterId, EatStrPotion: newRawCounter,
                UpdatedStats: updatedStats, FullActionRebroadcast: true),
            StatPotionKind.Dex => new TribeProgressZoneCommand(characterId, EatDexPotion: newRawCounter,
                UpdatedStats: updatedStats, FullActionRebroadcast: true),
            _ => new TribeProgressZoneCommand(characterId, EatElePotion: newRawCounter,
                UpdatedStats: updatedStats, FullActionRebroadcast: true)
        };

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped stat-potion mirror for character {CharacterId}",
                zone.MapId, characterId);

        await eventLog.LogAsync(StatPotionUsedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, unitsConsumed, StatPotionSuccessOutcome, null,
            cancellationToken);

        await eventLog.LogAsync(StatPotionSubTypeEventCode(kind, tier), EventLogCategory.ItemUse, accountId,
            characterId, null, null, null, null, null, item.ItemId, unitsConsumed, StatPotionSuccessOutcome,
            $"NewCount={newSubValue}", cancellationToken);

        var remaining = item.Quantity - unitsConsumed;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped stat-potion mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (stat potion) applied: item {ItemId} kind {Kind} tier {Tier}, {UnitsConsumed} unit(s) consumed, new counter {NewSubValue}",
            characterId, item.ItemId, kind, tier, unitsConsumed, newSubValue);

        return new UseInventoryItemResponse
            { Result = 0, Page = page, Index = index, Value = requestedValue, Value2 = 0 };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveLodTicketAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = LodTicketResolver.Resolve(state.Level, state.RebirthCount, item.Quantity, state.LodRounds);
        if (!resolved.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, LodRounds: resolved.NewLodRounds), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped LOD-round mirror for character {CharacterId}",
                zone.MapId, characterId);

        var consumed = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
        return consumed with { Value = resolved.NewLodRounds };
    }

    private static StatResetResolver.LevelBand? ResolveStatsClearBand(int itemId)
    {
        return itemId switch
        {
            1134 => StatResetResolver.LevelBand.UpTo99,
            1135 => StatResetResolver.LevelBand.Level100To112,
            1136 => StatResetResolver.LevelBand.Level113PlusNoGrade,
            1142 or 1459 => StatResetResolver.LevelBand.Level145PlusWithGrade,
            _ => null
        };
    }

    private static StatResetResolver.LevelBand? ResolveStatCleanseBand(int itemId)
    {
        return itemId switch
        {
            1137 => StatResetResolver.LevelBand.UpTo99,
            1138 => StatResetResolver.LevelBand.Level100To112,
            1139 or 8417 => StatResetResolver.LevelBand.Level113PlusNoGrade,
            1143 or 2022 => StatResetResolver.LevelBand.Level145PlusWithGrade,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveStatsClearAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, StatResetResolver.LevelBand requiredBand,
        CancellationToken cancellationToken)
    {
        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.Level2, out var actualBand) ||
            actualBand != requiredBand)
            return Fail(characterId, item, page, index);

        var resolved = StatResetResolver.ResolveStatsClear(state.StatVit, state.StatStr, state.StatInt, state.StatDex);

        var updatedStats = RecomputeStatsAfterReset(state, resolved.NewStatVit, resolved.NewStatStr,
            resolved.NewStatInt, resolved.NewStatDex);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: resolved.NewStatVit, StatStr: resolved.NewStatStr, StatInt: resolved.NewStatInt,
                StatDex: resolved.NewStatDex, StatPoints: state.StatPoints + resolved.RefundedPoints,
                Life: 1, Mana: 0, UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Stats-Clear mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveStatCleanseAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, StatResetResolver.LevelBand requiredBand, int selector,
        CancellationToken cancellationToken)
    {
        if (selector is < 1 or > 4)
            return Fail(characterId, item, page, index);

        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.Level2, out var actualBand) ||
            actualBand != requiredBand)
            return Fail(characterId, item, page, index);

        var currentValue = (StatResetResolver.StatSelector)selector switch
        {
            StatResetResolver.StatSelector.Strength => state.StatStr,
            StatResetResolver.StatSelector.Dexterity => state.StatDex,
            StatResetResolver.StatSelector.Vitality => state.StatVit,
            StatResetResolver.StatSelector.Intelligence => state.StatInt,
            _ => 0
        };

        var resolved = StatResetResolver.ResolveStatCleanse(currentValue);
        if (!resolved.Succeeded)
            return Fail(characterId, item, page, index);

        var newVit = state.StatVit;
        var newStr = state.StatStr;
        var newInt = state.StatInt;
        var newDex = state.StatDex;
        switch ((StatResetResolver.StatSelector)selector)
        {
            case StatResetResolver.StatSelector.Strength:
                newStr = resolved.NewValue;
                break;
            case StatResetResolver.StatSelector.Dexterity:
                newDex = resolved.NewValue;
                break;
            case StatResetResolver.StatSelector.Vitality:
                newVit = resolved.NewValue;
                break;
            case StatResetResolver.StatSelector.Intelligence:
                newInt = resolved.NewValue;
                break;
        }

        var updatedStats = RecomputeStatsAfterReset(state, newVit, newStr, newInt, newDex);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: newVit, StatStr: newStr, StatInt: newInt, StatDex: newDex,
                StatPoints: state.StatPoints + resolved.RefundedPoints, Life: 1, Mana: 0,
                UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Stat-Cleanse mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private EffectiveStats RecomputeStatsAfterReset(PlayerRuntimeState state, int statVit, int statStr,
        int statInt, int statDex, ConsumableContext? consumableOverride = null, int? rebirthCountOverride = null,
        short? level2Override = null)
    {
        var attributes = new CharacterBaseAttributes(statVit, statStr, statInt, statDex, state.Level, state.Tribe,
            state.PreviousTribe, state.Title, state.Halo, rebirthCountOverride ?? state.RebirthCount,
            level2Override ?? state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);
        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state, consumableOverride);
    }

    private static CharmChargeSpec? ResolveCharmFamily(int itemId)
    {
        return itemId switch
        {
            593 or 1218 => new CharmChargeSpec(ProtectionCharmCounterKind.Refine, 1),
            1103 or 1455 or 8418 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy, 1),
            1358 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy, 5),
            8103 or 8436 => new CharmChargeSpec(ProtectionCharmCounterKind.Costume, 1),
            828 or 837 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy2, 1),
            1166 or 8435 or 17033 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 1),
            1188 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 3),
            1237 or 8437 => new CharmChargeSpec(ProtectionCharmCounterKind.Wing, 1),
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveProtectionCharmAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, int requestedValue,
        ProtectionCharmCounterKind kind, int perUnitAmount, CancellationToken cancellationToken)
    {
        var bulkCount = BulkUseCoercion.Coerce(requestedValue, item.Quantity);

        var current = kind switch
        {
            ProtectionCharmCounterKind.Refine => state.ProtectForRefine,
            ProtectionCharmCounterKind.Destroy => state.ProtectForDestroy,
            ProtectionCharmCounterKind.Costume => state.ProtectForCostume,
            ProtectionCharmCounterKind.Destroy2 => state.ProtectForDestroy2,
            ProtectionCharmCounterKind.Halo => state.ProtectForHalo,
            ProtectionCharmCounterKind.Wing => state.ProtectForWing,
            _ => 0
        };

        var charged = kind == ProtectionCharmCounterKind.Halo
            ? ProtectionChargeResolver.ResolveCpProtCharmCharge(current, perUnitAmount, bulkCount, state.Halo)
            : ProtectionChargeResolver.ResolveCharmCharge(current, perUnitAmount, bulkCount);

        if (!charged.Succeeded)
            return Fail(characterId, item, page, index);

        var command = kind switch
        {
            ProtectionCharmCounterKind.Refine =>
                new TribeProgressZoneCommand(characterId, ProtectForRefine: charged.NewCounterValue),
            ProtectionCharmCounterKind.Destroy =>
                new TribeProgressZoneCommand(characterId, ProtectForDestroy: charged.NewCounterValue),
            ProtectionCharmCounterKind.Costume =>
                new TribeProgressZoneCommand(characterId, ProtectForCostume: charged.NewCounterValue),
            ProtectionCharmCounterKind.Destroy2 =>
                new TribeProgressZoneCommand(characterId, ProtectForDestroy2: charged.NewCounterValue),
            ProtectionCharmCounterKind.Halo =>
                new TribeProgressZoneCommand(characterId, ProtectForHalo: charged.NewCounterValue),
            ProtectionCharmCounterKind.Wing =>
                new TribeProgressZoneCommand(characterId, ProtectForWing: charged.NewCounterValue),
            _ => new TribeProgressZoneCommand(characterId)
        };

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped protection-charm mirror for character {CharacterId}",
                zone.MapId, characterId);

        var remaining = item.Quantity - charged.UnitsConsumed;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped protection-charm mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (protection charm) applied: item {ItemId} kind {Kind}, {UnitsConsumed} unit(s) consumed, new counter {NewCounterValue}",
            characterId, item.ItemId, kind, charged.UnitsConsumed, charged.NewCounterValue);

        return new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = charged.NewCounterValue, Value2 = charged.UnitsConsumed
        };
    }

    private static ScrollChargeSpec? ResolveScrollFamily(int itemId)
    {
        return itemId switch
        {
            1126 => new ScrollChargeSpec(ProtectionScrollCounterKind.ImproveItem, 1),
            1146 or 1231 => new ScrollChargeSpec(ProtectionScrollCounterKind.AddItem, 3),
            1147 => new ScrollChargeSpec(ProtectionScrollCounterKind.AddItem, 2),
            1148 => new ScrollChargeSpec(ProtectionScrollCounterKind.AddItem, 1),
            1149 or 1232 => new ScrollChargeSpec(ProtectionScrollCounterKind.HighItem, 3),
            1150 => new ScrollChargeSpec(ProtectionScrollCounterKind.HighItem, 2),
            1151 => new ScrollChargeSpec(ProtectionScrollCounterKind.HighItem, 1),
            1152 or 1233 => new ScrollChargeSpec(ProtectionScrollCounterKind.DropItemTime, 180),
            1153 => new ScrollChargeSpec(ProtectionScrollCounterKind.DropItemTime, 120),
            1154 => new ScrollChargeSpec(ProtectionScrollCounterKind.DropItemTime, 60),
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveProtectionScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, ProtectionScrollCounterKind kind, int fixedAmount,
        CancellationToken cancellationToken)
    {
        var current = kind switch
        {
            ProtectionScrollCounterKind.ImproveItem => state.ImproveItemValue,
            ProtectionScrollCounterKind.AddItem => state.AddItemValue,
            ProtectionScrollCounterKind.HighItem => state.HighItemValue,
            ProtectionScrollCounterKind.DropItemTime => state.DropItemTime,
            _ => 0
        };

        var charged = ProtectionChargeResolver.ResolveScrollCharge(current, fixedAmount);
        if (!charged.Succeeded)
            return Fail(characterId, item, page, index);

        var command = kind switch
        {
            ProtectionScrollCounterKind.ImproveItem =>
                new TribeProgressZoneCommand(characterId, ImproveItemValue: charged.NewCounterValue),
            ProtectionScrollCounterKind.AddItem =>
                new TribeProgressZoneCommand(characterId, AddItemValue: charged.NewCounterValue),
            ProtectionScrollCounterKind.HighItem =>
                new TribeProgressZoneCommand(characterId, HighItemValue: charged.NewCounterValue),
            ProtectionScrollCounterKind.DropItemTime =>
                new TribeProgressZoneCommand(characterId, DropItemTime: charged.NewCounterValue),
            _ => new TribeProgressZoneCommand(characterId)
        };

        if (!await zone.PostTribeProgressCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped protection-scroll mirror for character {CharacterId}",
                zone.MapId, characterId);

        var consumed = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);

        return kind == ProtectionScrollCounterKind.DropItemTime
            ? consumed with { Value = charged.NewCounterValue }
            : consumed;
    }

    private static bool IsDeathProtectionScroll(int itemId)
    {
        return itemId is 1108 or 7002 or 8416;
    }

    private async ValueTask<UseInventoryItemResponse> ResolveDeathProtectionScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        int newCounter;
        try
        {
            newCounter = await characters.AdjustDeathProtectionAsync(characterId, DeathProtectionScrollAmount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} death-protection scroll credit failed for item {ItemId}; item left untouched",
                characterId, item.ItemId);
            return Fail(characterId, item, page, index);
        }

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (death-protection scroll) applied: item {ItemId}, new counter {NewCounter}",
            characterId, item.ItemId, newCounter);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveAppearanceChangeScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int packedValue,
        CancellationToken cancellationToken)
    {
        var newHeadType = packedValue % 100 / 10 - 1;
        var newFaceType = packedValue / 100 - 1;

        if (newHeadType is < 0 or > 6 || newFaceType is < 0 or > 2)
            return Fail(characterId, item, page, index);

        try
        {
            await characters.UpdateAppearanceAsync(characterId, (byte)newHeadType, (byte)newFaceType,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} appearance-change scroll failed for item {ItemId}; item left untouched",
                characterId, item.ItemId);
            return Fail(characterId, item, page, index);
        }

        zone.PostCostumeCommand(new CostumeZoneCommand(characterId,
            NewHeadType: (byte)newHeadType,
            NewFaceType: (byte)newFaceType));

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (appearance-change scroll) applied: item {ItemId}, headType {HeadType}, faceType {FaceType}",
            characterId, item.ItemId, newHeadType, newFaceType);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveGenderScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int packedValue,
        CancellationToken cancellationToken)
    {
        var newGender = packedValue % 10 - 1;
        var newHeadType = packedValue % 100 / 10 - 1;
        var newFaceType = packedValue / 100 - 1;

        if (newGender < 0 || newHeadType is < 0 or > 6 || newFaceType is < 0 or > 2)
            return Fail(characterId, item, page, index);

        try
        {
            await characters.UpdateGenderAndAppearanceAsync(characterId, (byte)newGender, (byte)newHeadType,
                (byte)newFaceType, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} gender scroll failed for item {ItemId}; item left untouched",
                characterId, item.ItemId);
            return Fail(characterId, item, page, index);
        }

        zone.PostCostumeCommand(new CostumeZoneCommand(characterId,
            NewGender: (byte)newGender,
            NewHeadType: (byte)newHeadType,
            NewFaceType: (byte)newFaceType));

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (gender scroll) applied: item {ItemId}, gender {Gender}, headType {HeadType}, faceType {FaceType}",
            characterId, item.ItemId, newGender, newHeadType, newFaceType);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveInstantExpPillAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, CancellationToken cancellationToken)
    {
        switch (item.ItemId)
        {
            case 1489 when state.Level < 113 || state.Level2 != 0:
                return Fail(characterId, item, page, index);
            case 1490 when state.Level2 < 1:
                return Fail(characterId, item, page, index);
        }

        if (InstantExpPillFormulas.IsAtAbsoluteExpCeiling(state.Level, state.Experience))
            return Fail(characterId, item, page, index);

        var gain = item.ItemId switch
        {
            649 => InstantExpPillFormulas.ComputeLevelBandGain(state.Level, worldData.LevelsByLevel, 5),
            650 => InstantExpPillFormulas.ComputeLevelBandGain(state.Level, worldData.LevelsByLevel, 10),
            1489 => InstantExpPillFormulas.ComputeLevelBandGain(state.Level, worldData.LevelsByLevel, 3),
            1490 => InstantExpPillFormulas.ComputeRebirthTierGain(state.Level2),
            _ => 0
        };

        if (gain <= 0)
            return Fail(characterId, item, page, index);

        zone.GrantInstantExperience(state, gain);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (instant-EXP pill) applied: item {ItemId} granted {Gain} EXP",
            characterId, item.ItemId, gain);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolvePetExpBoostPillAsync(Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, byte page, byte index, ItemStack item,
        int requestedValue, CancellationToken cancellationToken)
    {
        var bulkCount = BulkUseCoercion.Coerce(requestedValue, item.Quantity);

        var charged = PetExpBoostPillResolver.ResolveCharge(state.PetExpX2Time, bulkCount);
        if (!charged.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, PetExpX2Time: charged.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Pet-EXP-boost-pill mirror for character {CharacterId}",
                zone.MapId, characterId);

        var remaining = item.Quantity - bulkCount;
        var packedValue = ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket);

        await eventLog.LogAsync(PetExpBoostPillUsedEventCode, EventLogCategory.CashItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, remaining, 0, $"Value={packedValue};Serial={item.Serial}",
            cancellationToken);

        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped Pet-EXP-boost-pill mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (pet EXP boost pill) applied: item {ItemId}, {BulkCount} unit(s) consumed, new counter {NewCounterValue}",
            characterId, item.ItemId, bulkCount, charged.NewCounterValue);

        return new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = charged.NewCounterValue, Value2 = bulkCount
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolvePetFoodAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, ItemStack item, int requestedValue,
        CancellationToken cancellationToken)
    {
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        if (petItemId == 0 || state.PetActivity < 1)
            return Fail(characterId, item, page, index);

        var bulkCount = BulkUseCoercion.Coerce(requestedValue, item.Quantity);

        var feed = PetFoodFeedResolver.Resolve(petItemId, state.PetGrowth, state.PetActivity, item.ItemId,
            bulkCount, worldData.ItemsById);

        if (feed.UnitsCredited < 1)
            return Fail(characterId, item, page, index);

        EffectiveStats? updatedStats = null;
        if (feed.TierIncreased)
            updatedStats = RecomputePetStats(state, petItemId, feed.NewGrowth, state.PetActivity);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, PetGrowth: feed.NewGrowth, UpdatedStats: updatedStats,
                    PetGrowStepBroadcast: feed.TierIncreased, FullActionRebroadcast: feed.TierIncreased),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped pet-food growth mirror for character {CharacterId}",
                zone.MapId, characterId);

        var remaining = item.Quantity - feed.UnitsCredited;
        var packedValue = ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket);
        await eventLog.LogAsync(PetFoodUsedEventCode, EventLogCategory.CashItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, feed.UnitsCredited, 0,
            $"Value={packedValue};Serial={item.Serial};NewGrowth={feed.NewGrowth}", cancellationToken);

        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped pet-food mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (pet food) applied: item {ItemId}, {UnitsCredited} unit(s) consumed, new pet growth {NewGrowth} (tier increased {TierIncreased})",
            characterId, item.ItemId, feed.UnitsCredited, feed.NewGrowth, feed.TierIncreased);

        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    private EffectiveStats RecomputePetStats(PlayerRuntimeState state, int petItemId, int newPetGrowth,
        int petActivity)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petContribution = PetGrowthCalculator.Compute(petItemId, newPetGrowth, petActivity, worldData.ItemsById);
        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveRebirthPillAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        if (!RebirthProgression.IsHighLevelExperienceFull(state.Level2, state.Exp2) ||
            state.RebirthCount >= RebirthProgression.MaxRebirthGeneration)
            return Fail(characterId, item, page, index);

        var newRebirthCount = state.RebirthCount + 1;
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, newRebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);

        var milestone = RebirthMilestoneRewards.Resolve(newRebirthCount, state.PreviousTribe);

        var response = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                    RebirthCount: newRebirthCount, Exp2: 0, UpdatedStats: updatedStats, RebirthBroadcast: true,
                    DropItems: milestone.Drops),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Rebirth-Pill mirror for character {CharacterId}",
                zone.MapId, characterId);

        if (milestone.ClusterNotice)
            worldNotice.Broadcast(RebirthMilestoneRewards.FormatTwelfthRebirthNotice(state.Name));

        logger.LogInformation(
            "Character {CharacterId} rebirth advanced to generation {NewRebirthCount} via item {ItemId}",
            characterId, newRebirthCount, item.ItemId);

        return response;
    }

    private async ValueTask<UseInventoryItemResponse> ResolveFactionNoticeAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = CashTimerResolver.ResolveFactionNotice(state.TribeNotifyScrollCount);
        if (!resolved.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: resolved.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Faction-Notice mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveTaiyanKeyAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = CashTimerResolver.ResolveTaiyanKey(state.Level, state.TaiyanKeyTimer);
        if (!resolved.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, TaiyanKeyTimer: resolved.NewValue), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Taiyan-Key mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int? ResolveDoubleExpTime1Amount(int itemId)
    {
        return itemId switch
        {
            539 or 1041 or 1421 => 180,
            1359 => 1800,
            _ => null
        };
    }

    private static int? ResolveDoubleExpTime2Amount(int itemId)
    {
        return itemId switch
        {
            1436 or 1458 or 7001 or 8414 => 180,
            1438 => 108,
            1439 or 7012 => 36,
            _ => null
        };
    }

    private static int? ResolveFightingGodForDestroyAmount(int itemId)
    {
        return itemId switch
        {
            1121 => 60,
            1122 => 120,
            1123 or 1234 => 180,
            _ => null
        };
    }

    private static int? ResolveDoubleKillNumTimeAmount(int itemId)
    {
        return itemId switch
        {
            1118 or 1454 or 8401 => 30,
            _ => null
        };
    }

    private static int? ResolveDoubleKillExpTimeAmount(int itemId)
    {
        return itemId switch
        {
            1119 or 1456 or 8402 => 30,
            _ => null
        };
    }

    private static int? ResolveDoubleKillBothAmount(int itemId)
    {
        return itemId switch
        {
            1120 or 1163 or 1186 => 30,
            1228 => 90,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveDoubleKillBothScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int addAmount,
        CancellationToken cancellationToken)
    {
        var addedNum = BankedCounterMath.AddNarrow(state.DoubleKillNumTime, addAmount);
        var addedExp = BankedCounterMath.AddNarrow(state.DoubleKillExpTime, addAmount);
        if (!addedNum.Succeeded || !addedExp.Succeeded)
            return Fail(characterId, item, page, index);

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = DoubleKillNumTimeSortCode, Value = addedNum.NewValue, Value2 = 0 });
        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = DoubleKillExpTimeSortCode, Value = addedExp.NewValue, Value2 = 0 });

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId,
                    DoubleKillNumTime: addedNum.NewValue,
                    DoubleKillExpTime: addedExp.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped DoubleKillBoth mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} DoubleKillBoth scroll applied: item {ItemId} +{AddAmount} min → NumTime={NumTime} ExpTime={ExpTime}",
            characterId, item.ItemId, addAmount, addedNum.NewValue, addedExp.NewValue);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int? ResolveStorageVaultRenewalDays(int itemId)
    {
        return itemId switch
        {
            1101 or 1130 or 2020 => 30,
            1140 => 60,
            1357 => 180,
            8408 => 7,
            _ => null
        };
    }

    private static int? ResolveHermitsVaultRenewalDays(int itemId)
    {
        return itemId switch
        {
            1102 or 1129 or 2019 => 30,
            807 or 8407 => 7,
            1141 => 60,
            1356 => 180,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveVaultRenewalAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int days,
        VaultRenewalTarget target, CancellationToken cancellationToken)
    {
        var currentExpiry = ReadVaultExpiry(state, target);

        if (!HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(currentExpiry, GameDate.Today(), days,
                out var newExpiry))
            return Fail(characterId, item, page, index);

        var command = target switch
        {
            VaultRenewalTarget.HermitsVault => new InventoryZoneCommand(characterId,
                ImmutableArray<InventoryContainerSnapshot>.Empty, null, InventoryDate: newExpiry),
            VaultRenewalTarget.StorageVault => new InventoryZoneCommand(characterId,
                ImmutableArray<InventoryContainerSnapshot>.Empty, null, StoreDate: newExpiry),
            _ => new InventoryZoneCommand(characterId, ImmutableArray<InventoryContainerSnapshot>.Empty, null,
                PetBagDate: newExpiry)
        };

        var posted = await zone.PostInventoryCommandAndWaitAsync(command, cancellationToken);

        if (!posted || ReadVaultExpiry(state, target) != newExpiry)
        {
            logger.LogError(
                "Zone {MapId} did not apply the {Target} renewal for character {CharacterId} (item {ItemId}, {Days}d, {Current} -> {New}): item kept, nothing granted",
                zone.MapId, target, characterId, item.ItemId, days, currentExpiry, newExpiry);
            return Fail(characterId, item, page, index);
        }

        logger.LogInformation(
            "Character {CharacterId} {Target} renewal applied: item {ItemId} +{Days} day(s), expiry {Current} -> {New}",
            characterId, target, item.ItemId, days, currentExpiry, newExpiry);

        var consumed = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);

        return consumed with { Value = newExpiry };
    }

    private static int ReadVaultExpiry(PlayerRuntimeState state, VaultRenewalTarget target)
    {
        return target switch
        {
            VaultRenewalTarget.HermitsVault => state.InventoryDate,
            VaultRenewalTarget.StorageVault => state.StoreDate,
            _ => state.PetBagDate
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveTimedCounterItemAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        int addAmount, int sort,
        Func<PlayerRuntimeState, int> getTimer,
        Func<int, int, TribeProgressZoneCommand> buildCommand,
        CancellationToken cancellationToken)
    {
        var current = getTimer(state);
        var added = BankedCounterMath.AddNarrow(current, addAmount);
        if (!added.Succeeded)
            return Fail(characterId, item, page, index);

        state.Session.Send(new AvatarStatUpdateResponse { Sort = sort, Value = added.NewValue, Value2 = 0 });

        if (!await zone.PostTribeProgressCommandAndWaitAsync(buildCommand(characterId, added.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped timed-counter (sort {Sort}) mirror for character {CharacterId}",
                zone.MapId, sort, characterId);

        logger.LogInformation(
            "Character {CharacterId} timed-counter item applied: item {ItemId} sort {Sort} +{AddAmount} min → new value {NewValue}",
            characterId, item.ItemId, sort, addAmount, added.NewValue);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveCpRandomBagAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var roll = Random.Shared.Next(100);

        if (roll > 40)
            return await ResolveDoubleKillBothScrollAsync(zone, state, characterId, page, index, item, 30,
                cancellationToken);

        int cpAmount;
        if (roll == 0)
        {
            cpAmount = Random.Shared.Next(2) == 0 ? 5_000 : 10_000;
        }
        else
        {
            var subRoll = Random.Shared.Next(10);
            cpAmount = subRoll switch { 0 => 3_000, <= 2 => 1_000, _ => 500 };
        }

        var added = BankedCounterMath.AddWideSafe(state.ContributionPoints, cpAmount);
        if (!added.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} CP-random-bag rejected: CP would exceed ceiling (current {Current} + {Amount})",
                characterId, state.ContributionPoints, cpAmount);
            return Fail(characterId, item, page, index);
        }

        logger.LogInformation(
            "Character {CharacterId} CP-random-bag applied: item {ItemId} roll {Roll} granted {CpAmount} CP → {NewCp}",
            characterId, item.ItemId, roll, cpAmount, added.NewValue);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, added.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP-random-bag CP mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveFortunePouchAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var rawGold = (1000 + Random.Shared.Next(4001)) * 100;

        if (rawGold > 200_000)
            rawGold = (int)(rawGold / 1.25347f);

        try
        {
            await characters.AdjustMoneyAsync(characterId, rawGold, 0, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} Fortune Pouch AdjustMoneyAsync failed for item {ItemId}; item left untouched",
                characterId, item.ItemId);
            return Fail(characterId, item, page, index);
        }

        state.Session.Send(new AvatarStatUpdateResponse { Sort = MoneySilverDeltaSort, Value = rawGold, Value2 = 0 });

        logger.LogInformation(
            "Character {CharacterId} Fortune Pouch applied: item {ItemId} granted {Gold} silver",
            characterId, item.ItemId, rawGold);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveRebirthResetScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        if (state.Level2 < 1)
            return Fail(characterId, item, page, index);

        var spCost = 100 * state.Level2;
        if (state.SkillPoints < spCost)
            return Fail(characterId, item, page, index);

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        foreach (var (_, stack) in equipmentContainer)
            if (stack.ItemId != 0)
                return Fail(characterId, item, page, index);

        const int resetStat = 1;
        const int resetStatPoints = 1775;

        var newEatLife = Math.Min(state.EatLifePotion, RebirthResetElixirCap);
        var newEatMana = Math.Min(state.EatManaPotion, RebirthResetElixirCap);
        var newEatStr = Math.Min(state.EatStrPotion, RebirthResetElixirCap);
        var newEatDex = Math.Min(state.EatDexPotion, RebirthResetElixirCap);
        var newEatEle =
            Math.Min(ElementalPotionPacking.DamageSubValue(state.EatElePotion), RebirthResetElixirCap) *
            ElementalPotionPacking.Modulus +
            Math.Min(ElementalPotionPacking.DefenseSubValue(state.EatElePotion), RebirthResetElixirCap);

        var consumableOverride = new ConsumableContext(newEatLife, newEatMana, newEatStr, newEatDex, newEatEle,
            state.HPBoost > 0, state.WarriorPill > 0, state.DmgBoost > 0, state.CriBoost > 0);

        var updatedStats = RecomputeStatsAfterReset(state, resetStat, resetStat, resetStat, resetStat,
            consumableOverride, 0, 0);

        var newEliteDungeonTime = Math.Max(0, state.EliteDungeonTime - RebirthResetZone101TimeRefund);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: resetStat, StatStr: resetStat, StatInt: resetStat, StatDex: resetStat,
                StatPoints: resetStatPoints, Level2: 0, Exp2: 0, RebirthCount: 0,
                SkillPoints: state.SkillPoints - spCost,
                Life: 1, Mana: 0,
                EatLifePotion: newEatLife, EatManaPotion: newEatMana, EatStrPotion: newEatStr,
                EatDexPotion: newEatDex, EatElePotion: newEatEle,
                EliteDungeonTime: newEliteDungeonTime,
                UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped rebirth-reset-scroll mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} rebirth-reset-scroll applied: level2={Level2} spCost={SpCost} eliteDungeonTime {Old}->{New}",
            characterId, state.Level2, spCost, state.EliteDungeonTime, newEliteDungeonTime);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveReductionSutraAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        int requestedSlot, CancellationToken cancellationToken)
    {
        if (requestedSlot is < 0 or >= SkillLearnResolver.MaxSlots)
        {
            logger.LogWarning(
                "Character {CharacterId} reduction-sutra slot {Slot} out of range",
                characterId, requestedSlot);
            return Fail(characterId, item, page, index);
        }

        var slot = (byte)requestedSlot;
        if (!state.LearnedSkills.TryGetValue(slot, out var learned) || learned.SkillId == 0)
        {
            logger.LogWarning(
                "Character {CharacterId} reduction-sutra slot {Slot} is empty",
                characterId, slot);
            return Fail(characterId, item, page, index);
        }

        var skillId = learned.SkillId;
        var gradeRefund = learned.Grade;

        var hotkeyWrites = ImmutableArray.CreateBuilder<HotkeySlotWrite>();
        foreach (var ((hkPage, hkIndex), hkSlot) in state.Hotkeys)
            if (hkSlot.Kind == HotkeyBindingKind.Skill && hkSlot.Value1 == skillId)
                hotkeyWrites.Add(new HotkeySlotWrite(hkPage, hkIndex, HotkeySlot.Empty));

        foreach (var write in hotkeyWrites)
            await characters.UpsertHotkeySlotAsync(characterId, write.Page, write.Index, 0, 0, 0,
                cancellationToken);

        await characters.UpsertSkillSlotAsync(characterId, slot, 0, 0, cancellationToken);

        var newSkillPoints = state.SkillPoints + gradeRefund;

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, new LearnedSkill(0, 0), newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped reduction-sutra skill-clear mirror (slot {Slot}) for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, slot, characterId);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, SkillPoints: newSkillPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped reduction-sutra SP mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (hotkeyWrites.Count > 0)
            if (!await zone.PostHotkeyMoveCommandAndWaitAsync(
                    new HotkeyMoveZoneCommand(characterId, hotkeyWrites.ToImmutable(), null),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} hotkey-move inbox full: dropped reduction-sutra hotkey-clear mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, characterId);

        var newAutoBuffSkill = state.AutoBuffSkill;
        var autoBuffChanged = false;
        for (var i = 0; i < newAutoBuffSkill.Length; i++)
            if (newAutoBuffSkill[i].SkillId == skillId)
            {
                newAutoBuffSkill = newAutoBuffSkill.SetItem(i, (0, 0));
                autoBuffChanged = true;
            }

        var newAutoHuntConfig = RemoveSkillFromAutoHuntConfig(state.AutoHuntConfig, skillId);
        if (newAutoHuntConfig is { } clearedConfig)
            await PersistAutoHuntConfigAsync(characterId, state.AutoHuntEnabled, clearedConfig, cancellationToken);

        ImmutableArray<(int SkillId, int Grade)>? registeredAutoBuffSkills =
            autoBuffChanged ? newAutoBuffSkill : null;

        if (autoBuffChanged || newAutoHuntConfig is not null)
            if (!zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId, registeredAutoBuffSkills,
                    NewAutoHuntConfig: newAutoHuntConfig)))
                logger.LogError(
                    "Zone {MapId} auto-buff inbox full: dropped reduction-sutra auto-buff/auto-hunt clear for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} reduction-sutra applied: cleared skill slot {Slot} (skillId {SkillId}), refunded {Grade} SP (total now {Total}), cleared {HotkeyCount} hotkey(s)",
            characterId, slot, skillId, gradeRefund, newSkillPoints, hotkeyWrites.Count);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolvePremiumServiceAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int days, CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var baseTime = state.PremiumExpireUtc > nowUtc ? state.PremiumExpireUtc : nowUtc;
        var newExpiry = baseTime + 86400 * days;

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, PremiumExpireUtc: newExpiry), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped premium-service mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} premium-service item {ItemId} applied: +{Days} day(s), expiry now {Expiry} (UTC)",
            characterId, item.ItemId, days, DateTimeOffset.FromUnixTimeSeconds(newExpiry).UtcDateTime);

        var consumed = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);

        return consumed with
        {
            Value = unchecked((int)(newExpiry & 0xFFFFFFFFL)), Value2 = unchecked((int)(newExpiry >> 32))
        };
    }

    private static int? ResolvePremiumDays(int itemId)
    {
        return itemId switch
        {
            PremiumService1DayA or PremiumService1DayB or PremiumService1DayC => 1,
            PremiumService3Days => 3,
            PremiumService7DaysA or PremiumService7DaysB => 7,
            PremiumService30Days => 30,
            _ => null
        };
    }

    private static int? ResolveAutoBuffScrollDays(int itemId)
    {
        return itemId switch
        {
            1201 or 2021 or 8406 => 7,
            1215 => 30,
            1216 or 8405 => 1,
            _ => null
        };
    }

    private static int? ResolveMountAbsorbMinutes(int itemId)
    {
        return itemId switch
        {
            613 => 60,
            1222 => 180,
            _ => null
        };
    }

    private static int? ResolveAutoHuntMinutesAmount(int itemId)
    {
        return itemId switch
        {
            574 or 2314 or 8403 => 300,
            722 => 180,
            _ => null
        };
    }

    private static int? ResolveAutoHuntDaysAmount(int itemId)
    {
        return itemId switch
        {
            610 or 686 or 8404 => 7,
            658 or 8105 => 1,
            687 => 15,
            1217 => 30,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveAutoHuntMinutesScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        var added = BankedCounterMath.AddNarrow(state.AutoHuntPaidMinuteBudget, minutes);
        if (!added.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, AutoHuntPaidMinuteBudget: added.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped auto-hunt-minutes-scroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} auto-hunt minute scroll applied: item {ItemId} +{Minutes} min, AutoHuntPaidMinuteBudget now {NewValue}",
            characterId, item.ItemId, minutes, added.NewValue);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveAutoHuntDaysScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int days, CancellationToken cancellationToken)
    {
        var baseDate = state.AutoHuntPaidDayBudget >= GameDate.Today()
            ? state.AutoHuntPaidDayBudget
            : GameDate.Today();
        if (!GameDate.TryAddDays(baseDate, days, out var newBudget))
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, AutoHuntPaidDayBudget: newBudget),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped auto-hunt-days-scroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} auto-hunt day scroll applied: item {ItemId} +{Days} day(s), AutoHuntPaidDayBudget now {NewBudget}",
            characterId, item.ItemId, days, newBudget);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int? ResolveMountDoubleExpMinutes(int itemId)
    {
        return itemId switch
        {
            1221 or 17034 or 8412 => 180,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveAutoBuffScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int days, CancellationToken cancellationToken)
    {
        var baseDate = state.AutoBuffTime >= GameDate.Today() ? state.AutoBuffTime : GameDate.Today();
        if (!GameDate.TryAddDays(baseDate, days, out var newDate))
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, AutoBuffTime: newDate), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped auto-buff-scroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} auto-buff scroll applied: item {ItemId} +{Days} day(s), AutoBuffTime now {NewDate}",
            characterId, item.ItemId, days, newDate);

        var remaining = item.Quantity - 1;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId,
                    ImmutableArray.Create(new InventoryContainerSnapshot(page, projected)), null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped auto-buff-scroll inventory mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = newDate, Value2 = 0 };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveMountAbsorbScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        var added = BankedCounterMath.AddNarrow(state.AnimalAbsorbTime, minutes);
        if (!added.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, AnimalAbsorbTime: added.NewValue), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped mount-absorb-scroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} mount-absorb scroll applied: item {ItemId} +{Minutes} min, AnimalAbsorbTime now {NewValue}",
            characterId, item.ItemId, minutes, added.NewValue);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveMountDoubleExpScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        var added = BankedCounterMath.AddNarrow(state.AnimalDoubleExp, minutes);
        if (!added.Succeeded)
            return Fail(characterId, item, page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, AnimalDoubleExp: added.NewValue), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped mount-double-exp-scroll mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} mount-double-exp scroll applied: item {ItemId} +{Minutes} min, AnimalDoubleExp now {NewValue}",
            characterId, item.ItemId, minutes, added.NewValue);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveBookOfAmnesiaAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        var totalRefund = 0;
        foreach (var (_, skill) in state.LearnedSkills)
            totalRefund += skill.Grade;

        var newSkillPoints = state.SkillPoints + totalRefund;

        foreach (var (slot, _) in state.LearnedSkills)
            await characters.UpsertSkillSlotAsync(characterId, slot, 0, 0, cancellationToken);

        var skillHotkeyWrites = ImmutableArray.CreateBuilder<HotkeySlotWrite>();
        foreach (var ((hkPage, hkIndex), hkSlot) in state.Hotkeys)
            if (hkSlot.Kind == HotkeyBindingKind.Skill)
                skillHotkeyWrites.Add(new HotkeySlotWrite(hkPage, hkIndex, HotkeySlot.Empty));

        foreach (var write in skillHotkeyWrites)
            await characters.UpsertHotkeySlotAsync(characterId, write.Page, write.Index, 0, 0, 0,
                cancellationToken);

        foreach (var (slot, _) in state.LearnedSkills)
            if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, new LearnedSkill(0, 0),
                    newSkillPoints)))
                logger.LogError(
                    "Zone {MapId} skill inbox full: dropped book-of-amnesia skill-clear mirror (slot {Slot}) for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, slot, characterId);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, SkillPoints: newSkillPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped book-of-amnesia skill-points mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (skillHotkeyWrites.Count > 0)
            if (!await zone.PostHotkeyMoveCommandAndWaitAsync(
                    new HotkeyMoveZoneCommand(characterId, skillHotkeyWrites.ToImmutable(), null),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} hotkey-move inbox full: dropped book-of-amnesia skill-hotkey-clear mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, characterId);

        var clearedAutoHuntConfig = state.AutoHuntConfig is { } currentAutoHuntConfig
            ? ClearAutoHuntSkillArrays(currentAutoHuntConfig)
            : EmptyAutoHuntConfig();
        await PersistAutoHuntConfigAsync(characterId, state.AutoHuntEnabled, clearedAutoHuntConfig,
            cancellationToken);

        if (!zone.PostAutoBuffCommand(
                new AutoBuffZoneCommand(characterId, AutoBuffSkillCodec.Empty,
                    NewAutoHuntConfig: clearedAutoHuntConfig)))
            logger.LogError(
                "Zone {MapId} auto-buff inbox full: dropped book-of-amnesia auto-buff/auto-hunt-clear mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} book-of-amnesia applied: refunded {Refund} skill points (total now {Total}), cleared {SkillCount} skill slots, {HotkeyCount} skill hotkeys",
            characterId, totalRefund, newSkillPoints, state.LearnedSkills.Count, skillHotkeyWrites.Count);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static AutoHunt EmptyAutoHuntConfig()
    {
        return new AutoHunt
        {
            BuffType = 0,
            BuffStore = new int[AutoHuntBuffStoreLength],
            HuntType = 0,
            AttackType = new int[AutoHuntAttackTypeLength],
            MonNum = 0,
            ItemType = 0,
            InvenCmd = 0,
            DeathCmd = 0,
            AnimalPreyCmd = 0,
            AnimalFoodCmd = 0
        };
    }

    private static AutoHunt ClearAutoHuntSkillArrays(AutoHunt config)
    {
        return config with
        {
            BuffStore = new int[AutoHuntBuffStoreLength], AttackType = new int[AutoHuntAttackTypeLength]
        };
    }

    private static AutoHunt? RemoveSkillFromAutoHuntConfig(AutoHunt? config, int skillId)
    {
        if (config is not { } current || skillId < 1)
            return null;

        var buffStore = (int[])current.BuffStore.Clone();
        var attackType = (int[])current.AttackType.Clone();
        var changed = false;

        for (var i = 0; i + 1 < buffStore.Length; i += 2)
            if (buffStore[i] == skillId)
            {
                buffStore[i] = 0;
                buffStore[i + 1] = 0;
                changed = true;
            }

        for (var i = 0; i + 1 < attackType.Length; i += 2)
            if (attackType[i] == skillId)
            {
                attackType[i] = 0;
                attackType[i + 1] = 0;
                changed = true;
            }

        if (!changed)
            return null;

        return current with { BuffStore = buffStore, AttackType = attackType };
    }

    private async ValueTask PersistAutoHuntConfigAsync(int characterId, bool enabled, AutoHunt config,
        CancellationToken cancellationToken)
    {
        var configBytes = new byte[AutoHunt.WireSize];
        config.Write(configBytes);
        await characters.SetAutoHuntAsync(characterId, enabled, configBytes, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ConsumeAndMirrorAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken,
        [CallerMemberName] string resolver = "")
    {
        var remaining = item.Quantity - 1;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item applied in {Resolver}: item {ItemId} consumed from slot {Page}:{Index} (remaining {Remaining})",
            characterId, resolver, item.ItemId, page, index, remaining);

        return response;
    }

    private ValueTask<UseInventoryItemResponse> ResolveWarPointBoxAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        zone.GrantWarPoints(characterId, WarPointBoxGrantAmount);
        return ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int? MountExpScrollAmount(int itemId)
    {
        return itemId switch
        {
            611 or 8427 => 3_000,
            612 or 8426 => 1_000,
            652 or 8428 => 5_000,
            17040 or 17041 => 100_000,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveMountExpScrollAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, int expAmount, bool requiresVip, CancellationToken cancellationToken)
    {
        if (state.AnimalIndex is < 10 or > 19)
            return Fail(characterId, item, page, index);

        var slot = state.AnimalIndex % 10;

        var garageItemId = state.MountGarage[slot];
        if (worldData.ItemsById.TryGetValue(garageItemId, out var mountDef) &&
            mountDef.Item.Sort == MountAnimalSortClassifier.NewMountItemSort)
            return Fail(characterId, item, page, index);

        var currentExp = state.MountAccumulatedExp[slot];
        if (currentExp >= MountStateResolver.MaxMountExp)
            return new UseInventoryItemResponse
                { Result = MountExpAlreadyMaxedResult, Page = page, Index = index, Value = 0, Value2 = 0 };

        if (requiresVip && state.UserSort < 1)
            return Fail(characterId, item, page, index);

        var newExp = Math.Min(currentExp + expAmount, MountStateResolver.MaxMountExp);

        if (!zone.PostMountCommand(new MountZoneCommand(characterId,
                MountExpSlot: slot, MountExpNewValue: newExp)))
            logger.LogWarning(
                "Zone {MapId} mount inbox full: dropped mount-EXP scroll mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private static int? GuildScrollBuffMinutes(int itemId)
    {
        return itemId switch
        {
            558 => 30,
            1211 or 8415 => 60,
            _ => null
        };
    }

    private static bool IsPetExpBoostPill(int itemId)
    {
        return itemId is 1190 or 17035 or 8413;
    }

    private static bool IsRebirthPill(int itemId)
    {
        return itemId is 632 or 1241 or 2462;
    }

    private UseInventoryItemResponse Fail(int characterId, ItemStack? item, byte page, byte index, int value = 0,
        [CallerMemberName] string resolver = "")
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Character {CharacterId} use-inventory-item rejected in {Resolver} (item {ItemId}, slot {Page}:{Index})",
                characterId, resolver, item?.ItemId, page, index);
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = value, Value2 = 0 };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private enum VaultRenewalTarget
    {
        HermitsVault,

        StorageVault,

        PetInventory
    }

    private enum StatPotionKind
    {
        Life,
        Mana,
        Str,
        Dex,
        ElementalDamage,
        ElementalDefense
    }

    private enum StatPotionTier
    {
        Single,
        TenStack,
        G12
    }

    private readonly record struct StatPotionSpec(StatPotionKind Kind, StatPotionTier Tier);

    private readonly record struct CharmChargeSpec(ProtectionCharmCounterKind Kind, int PerUnitAmount);

    private enum ProtectionCharmCounterKind
    {
        Refine,
        Destroy,
        Costume,
        Destroy2,
        Halo,
        Wing
    }

    private readonly record struct ScrollChargeSpec(ProtectionScrollCounterKind Kind, int FixedAmount);

    private enum ProtectionScrollCounterKind
    {
        ImproveItem,
        AddItem,
        HighItem,
        DropItemTime
    }
}
