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

    private const byte MixSkillSort = 23;

    private const byte MixSkillSort2 = 24;

    private const int DeathProtectionScrollAmount = 20;

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

    /// <summary>
    ///     Item 8110: War Point Box — grants a fixed lump of War Points on use.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5813-5821 — case 8110 adds 5 to <c>aWarPoint</c>
    ///     and broadcasts sort 905 (<c>S905UPDATE_WAR_POINT</c>), then decrements item quantity.
    /// </remarks>
    private const int WarPointBoxItemId = 8110;

    /// <summary>War Points granted per use of item <see cref="WarPointBoxItemId" />.</summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5814 — <c>wAvatar.aWarPoint += 5</c>.</remarks>
    private const int WarPointBoxGrantAmount = 5;

    /// <summary>Item ID for the Buff Duration Pill — adds 60 minutes to <c>BuffX2Time</c> (sort 42).</summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3063-3074 — <c>wAvatar.aBuffX2Time += 60</c> followed
    ///     by <c>SetUserBonus2()</c> to refresh the buff-duration multiplier.
    /// </remarks>
    private const int BuffDurationPillItemId = 1132;

    private const int BuffX2TimeStatSort = 42;

    /// <summary>
    ///     Minutes added to <see cref="PlayerRuntimeState.SilverTime" /> per Silver Ornament scroll use.
    /// </summary>
    /// <remarks>
    ///     Verified: Server/ts25zone/S04_MyWork03.cpp:3651 — <c>tAddTime = 180</c>.
    /// </remarks>
    private const int SilverScrollDurationMinutes = 180;

    /// <summary>
    ///     Minutes added to <see cref="PlayerRuntimeState.GoldTime" /> per Gold Ornament scroll use.
    /// </summary>
    /// <remarks>
    ///     Verified: Server/ts25zone/S04_MyWork03.cpp:3667 — <c>tAddTime = 240</c>.
    /// </remarks>
    private const int GoldScrollDurationMinutes = 240;

    // ──────────────────────────────────────────────────────────────────────────────
    // PvP kill-timer scroll constants
    // Réf. C++: Server/ts25zone/S04_MyWork03.cpp (item dispatch section)
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Sort code for <c>DoubleKillNumTime</c> countdown broadcasts (Scroll of Loyalty / Scroll of the Gods timer).
    ///     TODO(fenrir-gameplay-domain-engineer): Value 28 is a placeholder — the real B_AVATAR_CHANGE_INFO_2
    ///     sort code for <c>aDoubleKillNumTime</c> has NOT been confirmed from Server/ts25zone/S04_MyWork03.cpp.
    ///     Replace with the verified value once cpp-ts25-explorer confirms it.
    /// </summary>
    private const int DoubleKillNumTimeSortCode = 28;

    /// <summary>
    ///     Sort code for <c>DoubleKillExpTime</c> countdown broadcasts (Scroll of Battle / Scroll of the Gods timer).
    ///     TODO(fenrir-gameplay-domain-engineer): Value 29 is a placeholder — the real B_AVATAR_CHANGE_INFO_2
    ///     sort code for <c>aDoubleKillExpTime</c> has NOT been confirmed from Server/ts25zone/S04_MyWork03.cpp.
    ///     Replace with the verified value once cpp-ts25-explorer confirms it.
    /// </summary>
    private const int DoubleKillExpTimeSortCode = 29;

    /// <summary>Sort code for <c>DoubleKillNumTime2</c> per-kill counter broadcast. S030.</summary>
    /// <remarks>Réf. C++: Server/ts25zone/S07_MyGame02.cpp:2445-2448 — explicit sort 30 in legacy.</remarks>
    private const int DoubleKillNumTime2SortCode = 30;

    /// <summary>
    ///     Per-use charge added to <c>DoubleKillNumTime2</c> by items 1155/8438 (Crushed Demon Scroll).
    ///     Réf. C++: Server/ts25zone/S04_MyWork03.cpp — literal value 50.
    /// </summary>
    private const int DoubleKillNumTime2ChargeAmount = 50;

    private static readonly ImmutableHashSet<int> TribeConversionBookItemIds =
        ImmutableHashSet.Create(99014, 99015, 99016);

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

        if (ResolveDmgBoostMinutes(item.ItemId) is { } dbMin)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dbMin, 46, static s => s.DmgBoost,
                static (id, v) => new TribeProgressZoneCommand(id, DmgBoost: v),
                cancellationToken);

        if (ResolveHpBoostMinutes(item.ItemId) is { } hbMin)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                hbMin, 47, static s => s.HPBoost,
                static (id, v) => new TribeProgressZoneCommand(id, HPBoost: v),
                cancellationToken);

        if (ResolveCriBoostMinutes(item.ItemId) is { } cbMin)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                cbMin, 48, static s => s.CriBoost,
                static (id, v) => new TribeProgressZoneCommand(id, CriBoost: v),
                cancellationToken);

        if (ResolveWarriorPillMinutes(item.ItemId) is { } wpMin)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                wpMin, 91, static s => s.WarriorPill,
                static (id, v) => new TribeProgressZoneCommand(id, WarriorPill: v),
                cancellationToken);

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

        // DoubleKillNumTime+DoubleKillExpTime combined (Scroll of the Gods — adds minutes to both at once)
        if (ResolveDoubleKillBothAmount(item.ItemId) is { } dkBothAmount)
            return await ResolveDoubleKillBothScrollAsync(zone, state, characterId, page, index, item, dkBothAmount,
                cancellationToken);

        // DoubleKillNumTime only (Scroll of Loyalty)
        if (ResolveDoubleKillNumTimeAmount(item.ItemId) is { } dknAmount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dknAmount, DoubleKillNumTimeSortCode, static s => s.DoubleKillNumTime,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleKillNumTime: v),
                cancellationToken);

        // DoubleKillExpTime only (Scroll of Battle)
        if (ResolveDoubleKillExpTimeAmount(item.ItemId) is { } dkeAmount)
            return await ResolveTimedCounterItemAsync(zone, state, characterId, page, index, item,
                dkeAmount, DoubleKillExpTimeSortCode, static s => s.DoubleKillExpTime,
                static (id, v) => new TribeProgressZoneCommand(id, DoubleKillExpTime: v),
                cancellationToken);

        // DoubleKillNumTime2 (Crushed Demon Scroll — per-kill charge counter, NOT minutes)
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
            // Stats Convert Scroll: legacy sets mStatsConvertScroll = 1 then calls SetBasicAbilityFromEquip()
            // and SetHPMP(). The flag is only checked in S07_MyGame04 - Copy.cpp (excluded from build) —
            // dead feature in every production configuration. Effective observable behavior is:
            // consume the item, return success. No stat broadcast needed because nothing actually changes.
            // Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3353-3361.
            logger.LogInformation(
                "Character {CharacterId} use-inventory-item (Stats Convert Scroll {ItemId}): dead feature — item consumed",
                characterId, item.ItemId);
            return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
        }

        if (item.ItemId == WarPointBoxItemId)
            return await ResolveWarPointBoxAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (useItemRegistry?.Resolve(item, itemDefinition) is { } useItemHandler)
            return await useItemHandler.HandleAsync(
                new UseItemContext(zone, state, characterId, accountId, page, index, item, itemDefinition, value, session),
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
        return Math.Clamp(raw, 0, effectiveMax - current);
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
            1139 => StatResetResolver.LevelBand.Level113PlusNoGrade,
            1143 or 2022 or 8417 => StatResetResolver.LevelBand.Level145PlusWithGrade,
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
            1166 or 8435 or 17033 or 99405 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 1),
            1188 => new CharmChargeSpec(ProtectionCharmCounterKind.Halo, 3),
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

    /// <summary>
    ///     Item 1214 -- Appearance Change Scroll.
    /// </summary>
    /// <remarks>
    ///     Ref. legacy: Server/ts25zone/S04_MyWork03.cpp:3163-3179.
    ///     The client encodes the chosen appearance in <paramref name="packedValue" /> using two stacked decimal fields:
    ///     head index = (packedValue % 100 / 10) - 1  (client sends 1-based, we store 0-based, valid 0-6)
    ///     face index = (packedValue / 100) - 1        (client sends 1-based, we store 0-based, valid 0-2)
    ///     The legacy broadcast to nearby players was commented out; only the requesting player's own entry is updated.
    /// </remarks>
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

        // Mirror the DB change into the zone's in-memory state.
        // No broadcast to nearby players -- the legacy broadcast call is commented out at
        // Server/ts25zone/S04_MyWork03.cpp:3173-3178.
        zone.PostCostumeCommand(new CostumeZoneCommand(characterId,
            NewHeadType: (byte)newHeadType,
            NewFaceType: (byte)newFaceType));

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (appearance-change scroll) applied: item {ItemId}, headType {HeadType}, faceType {FaceType}",
            characterId, item.ItemId, newHeadType, newFaceType);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Item 1171 -- Gender Scroll.
    /// </summary>
    /// <remarks>
    ///     Ref. legacy: Server/ts25zone/S04_MyWork03.cpp:3175-3193.
    ///     Same packed-value encoding as item 1214 (Appearance Change Scroll) but the lowest decimal digit
    ///     also carries the gender:
    ///         gender   = (packedValue % 10) - 1        (1-based on wire, stored 0-based)
    ///         headType = (packedValue % 100 / 10) - 1  (1-based on wire, stored 0-based, valid 0-6)
    ///         faceType = (packedValue / 100) - 1       (1-based on wire, stored 0-based, valid 0-2)
    ///     No range is enforced on gender in legacy (the field is stored verbatim) -- we only reject encoding
    ///     values that decode to a negative index (i.e. the digit was 0, meaning no 1-based value was sent).
    ///     No broadcast to nearby players -- the legacy broadcast call is commented out at
    ///     Server/ts25zone/S04_MyWork03.cpp:3185-3192, matching item 1214 behavior.
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveGenderScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int packedValue,
        CancellationToken cancellationToken)
    {
        var newGender = packedValue % 10 - 1;
        var newHeadType = packedValue % 100 / 10 - 1;
        var newFaceType = packedValue / 100 - 1;

        // Gender: no legacy range check, but a decoded value < 0 means bad 1-based encoding.
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

        // Mirror the DB change into the zone's in-memory state.
        // No broadcast to nearby players -- the legacy broadcast call is commented out at
        // Server/ts25zone/S04_MyWork03.cpp:3185-3192.
        zone.PostCostumeCommand(new CostumeZoneCommand(characterId,
            NewGender: (byte)newGender,
            NewHeadType: (byte)newHeadType,
            NewFaceType: (byte)newFaceType));

        logger.LogInformation(
            "Character {CharacterId} use-inventory-item (gender scroll) applied: item {ItemId}, gender {Gender}, headType {HeadType}, faceType {FaceType}",
            characterId, item.ItemId, newGender, newHeadType, newFaceType);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Instant-EXP pills 649, 650, 1489, and 1490.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4112-4220 (LNW33-gated block, always active in ReleaseEU33).
    ///     Legacy also gates all four items on <c>wAuth.ExpFlag</c> (a server-side anti-cheat EXP lock).
    ///     Fenrir has no equivalent flag — that gate is omitted as a deliberate Fenrir divergence.
    ///     All formula logic is in <see cref="InstantExpPillFormulas" />.
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveInstantExpPillAsync(
        Zone zone, PlayerRuntimeState state, int characterId, byte page, byte index,
        ItemStack item, CancellationToken cancellationToken)
    {
        // Per-item level/rebirth gates (Server/ts25zone/S04_MyWork03.cpp:4119-4140)
        switch (item.ItemId)
        {
            case 1489 when state.Level < 113 || state.Level2 != 0:
                return Fail(characterId, item, page, index);
            case 1490 when state.Level2 < 1:
                return Fail(characterId, item, page, index);
        }

        // Absolute EXP ceiling: level 145 + main EXP == 2,000,000,000 means every pool is full
        // (Server/ts25zone/S04_MyWork03.cpp:4150-4155)
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

    /// <summary>
    ///     Returns the minutes to add to <c>DoubleKillNumTime</c> for Scroll of Loyalty items.
    ///     Does NOT cover the combined Scroll-of-the-Gods items (see <see cref="ResolveDoubleKillBothAmount"/>).
    ///     Réf. C++: Server/ts25zone/S04_MyWork03.cpp — items 1118/1454/8401 add 30 min to aDoubleKillNumTime.
    /// </summary>
    private static int? ResolveDoubleKillNumTimeAmount(int itemId) => itemId switch
    {
        1118 or 1454 or 8401 => 30,
        _ => null
    };

    /// <summary>
    ///     Returns the minutes to add to <c>DoubleKillExpTime</c> for Scroll of Battle items.
    ///     Does NOT cover the combined Scroll-of-the-Gods items (see <see cref="ResolveDoubleKillBothAmount"/>).
    ///     Réf. C++: Server/ts25zone/S04_MyWork03.cpp — items 1119/1456/8402 add 30 min to aDoubleKillExpTime.
    /// </summary>
    private static int? ResolveDoubleKillExpTimeAmount(int itemId) => itemId switch
    {
        1119 or 1456 or 8402 => 30,
        _ => null
    };

    /// <summary>
    ///     Returns the minutes to add to BOTH <c>DoubleKillNumTime</c> AND <c>DoubleKillExpTime</c>
    ///     for the combined Scroll-of-the-Gods family.
    ///     Réf. C++: Server/ts25zone/S04_MyWork03.cpp —
    ///     items 1120/1163/1186 add 30 min to both; item 1228 adds 90 min to both.
    /// </summary>
    private static int? ResolveDoubleKillBothAmount(int itemId) => itemId switch
    {
        1120 or 1163 or 1186 => 30,
        1228 => 90,
        _ => null
    };

    /// <summary>
    ///     Applies a Scroll of the Gods variant that adds <paramref name="addAmount"/> minutes to both
    ///     <c>DoubleKillNumTime</c> and <c>DoubleKillExpTime</c> atomically, then consumes the item.
    ///     Fails (without consuming) if either counter would overflow <see cref="BankedCounterMath.AddNarrow"/>.
    /// </summary>
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

    private static int? ResolveDmgBoostMinutes(int itemId) => itemId switch
    {
        1191 => 180, 1192 => 90, 1193 => 30, _ => null
    };

    private static int? ResolveHpBoostMinutes(int itemId) => itemId switch
    {
        1194 => 180, 1195 => 90, 1196 => 30, _ => null
    };

    private static int? ResolveCriBoostMinutes(int itemId) => itemId switch
    {
        1197 => 180, 1198 => 90, 1199 => 30, _ => null
    };

    private static int? ResolveWarriorPillMinutes(int itemId) => itemId switch
    {
        626 or 17037 => 180, 627 => 90, 628 => 30, _ => null
    };

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

    /// <summary>
    ///     Resolves item 99101 (CP Random Bag).
    ///     Three outcome tiers are rolled from a uniform 0–99 range:
    ///     <list type="bullet">
    ///         <item>Roll 0 (1 %): jackpot — grants either 5 000 or 10 000 CP with equal probability.</item>
    ///         <item>Roll 1–40 (40 %): grants 3 000 CP on sub-roll 0, 1 000 CP on sub-rolls 1–2, or 500 CP otherwise.</item>
    ///         <item>Roll 41–99 (59 %): adds 30 minutes to both <c>DoubleKillNumTime</c> and <c>DoubleKillExpTime</c>.</item>
    ///     </list>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5233-5374 (rand_mir() % 100 dispatch).
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ResolveCpRandomBagAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var roll = Random.Shared.Next(100);

        if (roll > 40)
        {
            // 59 % — +30 min to both DoubleKill timers; delegates to the existing dual-counter method.
            return await ResolveDoubleKillBothScrollAsync(zone, state, characterId, page, index, item, 30,
                cancellationToken);
        }

        // CP outcome — determine amount then grant.
        int cpAmount;
        if (roll == 0)
        {
            // 1 % jackpot: 50/50 between 5 000 and 10 000 CP.
            cpAmount = Random.Shared.Next(2) == 0 ? 5_000 : 10_000;
        }
        else
        {
            // 40 % tier: sub-roll within 0–9.
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
                new TribeProgressZoneCommand(characterId, ContributionPoints: added.NewValue),
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
        foreach (var (slot, stack) in equipmentContainer)
            if (slot != PetSlots.EquipmentSlot && stack.ItemId != 0)
                return Fail(characterId, item, page, index);

        const int resetStat = 1;
        const int resetStatPoints = 1775;

        var updatedStats = RecomputeStatsAfterReset(state, resetStat, resetStat, resetStat, resetStat);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                StatVit: resetStat, StatStr: resetStat, StatInt: resetStat, StatDex: resetStat,
                StatPoints: resetStatPoints, Level2: 0, Exp2: 0, RebirthCount: 0,
                SkillPoints: state.SkillPoints - spCost,
                UpdatedStats: updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped rebirth-reset-scroll mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} rebirth-reset-scroll applied: level2={Level2} spCost={SpCost}",
            characterId, state.Level2, spCost);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveReductionSutraAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        int requestedSlot, CancellationToken cancellationToken)
    {
        // Validate slot range [0, 39] — out-of-range is malformed; return Fail to avoid disconnecting
        // legitimate retries but don't proceed (legacy calls Quit() here,
        // Server/ts25zone/S04_MyWork03.cpp:2010-2013).
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

        // Collect hotkeys bound to this specific skill and clear them
        // (HotkeySlot.Value1 carries the skill/item id for Skill-kind bindings)
        var hotkeyWrites = ImmutableArray.CreateBuilder<HotkeySlotWrite>();
        foreach (var ((hkPage, hkIndex), hkSlot) in state.Hotkeys)
            if (hkSlot.Kind == HotkeyBindingKind.Skill && hkSlot.Value1 == skillId)
                hotkeyWrites.Add(new HotkeySlotWrite(hkPage, hkIndex, HotkeySlot.Empty));

        // Persist DB changes: hotkey clears, then skill slot clear
        foreach (var write in hotkeyWrites)
            await characters.UpsertHotkeySlotAsync(characterId, write.Page, write.Index, 0, 0, 0,
                cancellationToken);

        await characters.UpsertSkillSlotAsync(characterId, slot, 0, 0, cancellationToken);

        var newSkillPoints = state.SkillPoints + gradeRefund;

        // Mirror skill-slot clear into zone — one slot only
        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, slot, new LearnedSkill(0, 0), newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped reduction-sutra skill-clear mirror (slot {Slot}) for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, slot, characterId);

        // Mirror SP refund
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, SkillPoints: newSkillPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped reduction-sutra SP mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Mirror hotkey clears
        if (hotkeyWrites.Count > 0)
            if (!await zone.PostHotkeyMoveCommandAndWaitAsync(
                    new HotkeyMoveZoneCommand(characterId, hotkeyWrites.ToImmutable(), null),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} hotkey-move inbox full: dropped reduction-sutra hotkey-clear mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, characterId);

        // Clear auto-buff slots that reference this skill
        var newAutoBuffSkill = state.AutoBuffSkill;
        var autoBuffChanged = false;
        for (var i = 0; i < newAutoBuffSkill.Length; i++)
            if (newAutoBuffSkill[i].SkillId == skillId)
            {
                newAutoBuffSkill = newAutoBuffSkill.SetItem(i, (0, 0));
                autoBuffChanged = true;
            }

        if (autoBuffChanged)
            if (!zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId, newAutoBuffSkill)))
                logger.LogError(
                    "Zone {MapId} auto-buff inbox full: dropped reduction-sutra auto-buff clear for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                    zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} reduction-sutra applied: cleared skill slot {Slot} (skillId {SkillId}), refunded {Grade} SP (total now {Total}), cleared {HotkeyCount} hotkey(s)",
            characterId, slot, skillId, gradeRefund, newSkillPoints, hotkeyWrites.Count);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Premium-service consumables: items 2292/8420/8001 (+1 day), 8002 (+3 days),
    ///     8421/8003 (+7 days), 2138 (+30 days).
    /// </summary>
    /// <remarks>
    ///     Uses Unix-timestamp arithmetic (+86400 seconds per day), flooring the base to "now" when the
    ///     character's current premium has already expired — matching the
    ///     <c>USE_PREMIUM_LONGTIME</c>-branch behaviour from
    ///     Server/ts25zone/S04_MyWork03.cpp:2480-2545.
    ///     <c>USE_PREMIUM_LONGTIME</c> is defined unconditionally in the repository's one buildable
    ///     configuration (see Server/Header/DEFINE.h).
    /// </remarks>
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

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
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

    /// <summary>
    ///     Maps auto-buff scroll item IDs to the number of calendar days they add to <c>AutoBuffTime</c>.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3136-3153 — <c>WUSE_ITEM_1201</c> block.
    ///     Items 1201/2021/8406 add 7 days; 1215 adds 30 days; 1216/8405 add 1 day.
    /// </remarks>
    private static int? ResolveAutoBuffScrollDays(int itemId) => itemId switch
    {
        1201 or 2021 or 8406 => 7,
        1215 => 30,
        1216 or 8405 => 1,
        _ => null
    };

    /// <summary>
    ///     Maps mount-absorb scroll item IDs to the number of minutes they add to <c>AnimalAbsorbTime</c>.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3364-3377 — <c>WUSE_ITEM_613</c> block.
    ///     Item 613 adds 60 min; item 1222 adds 180 min.
    /// </remarks>
    private static int? ResolveMountAbsorbMinutes(int itemId) => itemId switch
    {
        613 => 60,
        1222 => 180,
        _ => null
    };

    /// <summary>
    ///     Maps auto-hunt minute-budget scroll item IDs to the number of minutes they add to
    ///     <c>AutoHuntPaidMinuteBudget</c> (<c>aAutoTime2</c>).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>WUSE_ITEM_574</c> block.
    ///     Items 574/2314/8403 add 300 minutes; item 722 adds 180 minutes.
    /// </remarks>
    private static int? ResolveAutoHuntMinutesAmount(int itemId) => itemId switch
    {
        574 or 2314 or 8403 => 300,
        722 => 180,
        _ => null
    };

    /// <summary>
    ///     Maps auto-hunt day-budget scroll item IDs to the number of days they add to
    ///     <c>AutoHuntPaidDayBudget</c> (<c>aAutoTime</c>).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>WUSE_ITEM_610</c> block.
    ///     Items 610/686/8404 add 7 days; items 658/8105 add 1 day; item 687 adds 15 days; item 1217 adds 30 days.
    /// </remarks>
    private static int? ResolveAutoHuntDaysAmount(int itemId) => itemId switch
    {
        610 or 686 or 8404 => 7,
        658 or 8105 => 1,
        687 => 15,
        1217 => 30,
        _ => null
    };

    /// <summary>
    ///     Adds <paramref name="minutes" /> to the character's paid auto-hunt minute budget,
    ///     capped at <see cref="BankedCounterMath.GlobalCeiling" />.
    ///     The zone sends the updated value to the client via stat-update sort 62 inside the mirror handler.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>WUSE_ITEM_574</c>/<c>WUSE_ITEM_722</c> blocks.
    ///     <c>wCheckAdd</c> rejects when addition would overflow the global ceiling.
    ///     Legacy sends <c>B_AVATAR_CHANGE_INFO_2(tUserInfo, 62, wAvatar.aAutoTime2)</c> immediately after update.
    /// </remarks>
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

    /// <summary>
    ///     Adds <paramref name="days" /> calendar days to the character's paid auto-hunt day budget.
    ///     The base date is the later of today and the current <c>AutoHuntPaidDayBudget</c>, so
    ///     consecutive uses always stack forward rather than overwriting an active subscription.
    ///     The zone sends the updated value to the client via stat-update sort 61 inside the mirror handler.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3740-3795 — <c>WUSE_ITEM_610</c> block.
    ///     <c>ReturnAddDate</c> returns -1 when the projected date overflows; that maps to a <c>Fail</c>
    ///     here exactly as <c>wUseInvFail()</c> does in the legacy.
    ///     Legacy sends <c>B_AVATAR_CHANGE_INFO_2(tUserInfo, 61, wAvatar.aAutoTime)</c> immediately after update.
    /// </remarks>
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

    /// <summary>
    ///     Maps double-mount-EXP scroll item IDs to the number of minutes they add to <c>AnimalDoubleExp</c>.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3379-3388 — <c>WUSE_ITEM_1221</c> block.
    ///     All three item IDs add 180 min.
    /// </remarks>
    private static int? ResolveMountDoubleExpMinutes(int itemId) => itemId switch
    {
        1221 or 17034 or 8412 => 180,
        _ => null
    };

    /// <summary>
    ///     Extends the character's auto-buff subscription by <paramref name="days" /> calendar days.
    ///     The base date is the later of today and the current <c>AutoBuffTime</c>, so consecutive uses
    ///     always stack forward. The new date is returned to the client as <c>UseInventoryItemResponse.Value</c>.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3136-3162 — <c>WUSE_ITEM_1201</c> block.
    ///     <c>ReturnAddDate</c> returns -1 when the projected date overflows <see cref="GameDate.Invalid" />;
    ///     that maps to a <c>Fail</c> here exactly as <c>wUseInvFail()</c> does in the legacy.
    ///     The response carries <c>r-&gt;tValue = newDate</c>; no separate stat-update packet is sent.
    /// </remarks>
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

        // Consume item and mirror to SQL/zone; build the response manually so Value carries the new date,
        // matching the legacy r->tValue = newDate behaviour (Server/ts25zone/S04_MyWork03.cpp:3147).
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

    /// <summary>
    ///     Adds <paramref name="minutes" /> to the character's mount-absorb time counter,
    ///     capped at <see cref="BankedCounterMath.GlobalCeiling" />.
    ///     The zone broadcasts the updated value to the client via stat-update sort 78.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3364-3377 — <c>WUSE_ITEM_613</c> block.
    ///     <c>wCheckAdd</c> rejects (legacy fail) when the addition would overflow the global ceiling.
    /// </remarks>
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

    /// <summary>
    ///     Adds 180 minutes to the character's double-mount-EXP counter,
    ///     capped at <see cref="BankedCounterMath.GlobalCeiling" />.
    ///     <c>AnimalDoubleExp</c> is in-memory only (not persisted), so no write-behind is triggered.
    ///     <see cref="TimedBuffCountdownSystem" /> broadcasts sort-75 decrements during normal tick processing.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3379-3388 — <c>WUSE_ITEM_1221</c> block.
    ///     Legacy has no B_AVATAR_CHANGE_INFO_2 call here; the client infers activation from the success response.
    /// </remarks>
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

        if (!zone.PostAutoBuffCommand(
                new AutoBuffZoneCommand(characterId, AutoBuffSkillCodec.Empty,
                    ClearAutoHuntConfig: true)))
            logger.LogError(
                "Zone {MapId} auto-buff inbox full: dropped book-of-amnesia auto-buff/auto-hunt-clear mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} book-of-amnesia applied: refunded {Refund} skill points (total now {Total}), cleared {SkillCount} skill slots, {HotkeyCount} skill hotkeys",
            characterId, totalRefund, newSkillPoints, state.LearnedSkills.Count, skillHotkeyWrites.Count);

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

    /// <summary>
    ///     Resolves item 8110 (War Point Box): grants <see cref="WarPointBoxGrantAmount" /> War Points and
    ///     consumes the item.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5813-5821 — adds 5 to <c>aWarPoint</c>, broadcasts
    ///     <c>B_AVATAR_CHANGE_INFO_2</c> with sort 905 (<c>S905UPDATE_WAR_POINT</c>), then calls
    ///     <c>DecreaseQuantity</c>. The sort-905 broadcast is handled by <see cref="Zone.GrantWarPoints" />;
    ///     item removal is handled by <see cref="ConsumeAndMirrorAsync" />.
    /// </remarks>
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
        if (requiresVip && state.UserSort < 1)
            return Fail(characterId, item, page, index);

        if (state.AnimalIndex is < 10 or > 19)
            return Fail(characterId, item, page, index);

        var slot = state.AnimalIndex % 10;

        var garageItemId = state.MountGarage[slot];
        if (worldData.ItemsById.TryGetValue(garageItemId, out var mountDef) &&
            mountDef.Item.Sort == MountAnimalSortClassifier.NewMountItemSort)
            return Fail(characterId, item, page, index);

        var currentExp = state.MountAccumulatedExp[slot];
        if (currentExp >= MountStateResolver.MaxMountExp)
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
