using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
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
    UseItemHandlerRegistry? useItemRegistry = null) : IUseInventoryItemService
{
    private const byte BottleSort = 26;

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

    private const short TeleportRecallScrollUsedEventCode = 1;

    private const byte TeleportRecallScrollSuccessOutcome = 1;

    private const short StatPotionUsedEventCode = 4;

    private const byte StatPotionSuccessOutcome = 1;

    private static readonly ImmutableHashSet<int> TribeConversionBookItemIds =
        ImmutableHashSet.Create(99014, 99015, 99016);

    public async ValueTask<UseInventoryItemResponse?> ResolveAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, int value, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
            return Fail(characterId, itemStack, page, index, value);

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

        if (useItemRegistry?.Resolve(item, itemDefinition) is { } useItemHandler)
            return await useItemHandler.HandleAsync(
                new UseItemContext(zone, state, characterId, accountId, page, index, item, itemDefinition, value),
                cancellationToken);

        return Unrecognized(state, characterId, accountId, item);
    }

    private UseInventoryItemResponse? Unrecognized(PlayerRuntimeState state, int characterId, int accountId,
        ItemStack item)
    {
        logger.LogError(
            "Character {CharacterId} ({CharacterName}, account {AccountId}, shard {ShardId}) use-inventory-item disconnect: item {ItemId} matched no recognized dispatch branch",
            characterId, state.Name, accountId, options.Value.ShardId, item.ItemId);
        return null;
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
            return Fail(characterId, item, page, index);

        var guild = await guilds.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
            return Fail(characterId, item, page, index);

        try
        {
            var topUp = GuildBuffTopUp.Apply(guild, minutes, DateTimeOffset.UtcNow);
            await guilds.SetBuffAsync(guildId, topUp.BuffType, topUp.BuffState, topUp.BuffTime,
                topUp.BuffTimeForDiff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild scroll recharge failed for guild {GuildId}", characterId, guildId);
            return Fail(characterId, item, page, index);
        }

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
            state.DmgBoost > 0);
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
            1136 => StatResetResolver.LevelBand.Level113PlusNoRebirth,
            1142 or 1459 => StatResetResolver.LevelBand.Level145PlusWithRebirth,
            _ => null
        };
    }

    private static StatResetResolver.LevelBand? ResolveStatCleanseBand(int itemId)
    {
        return itemId switch
        {
            1137 => StatResetResolver.LevelBand.UpTo99,
            1138 => StatResetResolver.LevelBand.Level100To112,
            1139 => StatResetResolver.LevelBand.Level113PlusNoRebirth,
            1143 or 2022 or 8417 => StatResetResolver.LevelBand.Level145PlusWithRebirth,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> ResolveStatsClearAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, StatResetResolver.LevelBand requiredBand,
        CancellationToken cancellationToken)
    {
        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.RebirthCount, out var actualBand) ||
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

        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.RebirthCount, out var actualBand) ||
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
        int statInt, int statDex, ConsumableContext? consumableOverride = null)
    {
        var attributes = new CharacterBaseAttributes(statVit, statStr, statInt, statDex, state.Level, state.Tribe,
            state.PreviousTribe, state.Title, state.Halo, state.RebirthCount, state.Level2);
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
            1103 or 1358 or 1455 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy, 1),
            8418 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy, 5),
            8103 or 8436 => new CharmChargeSpec(ProtectionCharmCounterKind.Costume, 1),
            828 or 837 => new CharmChargeSpec(ProtectionCharmCounterKind.Destroy2, 1),
            1166 or 1188 or 8435 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 1),
            17033 or 99405 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 3),
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
                    FullActionRebroadcast: feed.TierIncreased), cancellationToken))
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
        Halo
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
