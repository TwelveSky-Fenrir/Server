using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class UseInventoryItemService(
    ICharacterRepository characters,
    IGuildRepository guilds,
    ICashRepository cash,
    IOfflineShopRepository offlineShops,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    ILogger<UseInventoryItemService> logger) : IUseInventoryItemService
{
    private const byte BottleSort = 26;

    /// <summary>
    ///     game.CashLog.Reason for a GP ticket redemption credit -- distinct from Reason=1 (cash-shop
    ///     purchase debit, see <c>BuyCashItemService</c>). Reason is an unenforced, caller-owned byte (no DB
    ///     lookup table), same posture as game.EventLog.EventCode below.
    /// </summary>
    private const byte GpTicketCashCreditReason = 2;

    /// <summary>
    ///     game.EventLog.EventCode for a GP ticket redemption -- an app-owned numbering scheme with no
    ///     central catalog yet (this is the first Application-layer caller of <see cref="IEventLogRepository" />;
    ///     see game.EventLog.sql's own "EventCode is an app-owned numbering scheme" comment). Picked as an
    ///     arbitrary small value scoped to this one credit path; a future central event-code registry should
    ///     supersede this constant rather than silently reusing its numeric value for something unrelated.
    /// </summary>
    private const short GpTicketRedeemedEventCode = 1;

    /// <summary>
    ///     game.EventLog.EventCode for a proxy-shop rental extension, scoped independently within
    ///     <see cref="EventLogCategory.CashItemUse" /> -- reusing the numeral 1 here does not collide with
    ///     <see cref="GpTicketRedeemedEventCode" />'s own 1 in <see cref="EventLogCategory.Currency" />, since
    ///     EventCode is only ever caller-interpreted alongside its Category (see game.EventLog.sql's own
    ///     "app-owned numbering scheme" comment), but a distinct value is still picked here for readability.
    /// </summary>
    private const short ProxyShopRentalExtensionEventCode = 2;

    private const int LodTicketItemId = 1434;
    private const int FactionNoticeItemId = 566;
    private const int TaiyanKeyItemId = 1049;

    public async ValueTask<UseInventoryItemResponse> ResolveAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, int value, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
            return Fail(page, index);

        if (itemDefinition.Item.Sort == BottleSort)
            return await ResolveBottleAsync(zone, state, characterId, page, index, item, cancellationToken);

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

        if (IsTribeTransferScroll(item.ItemId))
            return await ResolveTribeTransferScrollAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        if (ProxyShopRentalExtensionResolver.ExtensionDaysFor(item.ItemId) is not null)
            return await ResolveProxyShopRentalExtensionAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        return Fail(page, index);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveBottleAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = BottleResolver.ResolveAcquire(state.BottleSlots, item.ItemId);
        if (resolved.Outcome == BottleResolver.AcquireOutcome.Rejected)
            return Fail(page, index);

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

        return response;
    }

    /// <summary>
    ///     GP ticket items (world.Items 723 "500 gp ticket" / 725 "100 gp ticket") credit a fixed, server-
    ///     authoritative amount (500/100) of cash-shop currency to the account, then consume the entire
    ///     stack in the slot -- not one unit per credit. The legacy 723/725 case labels are reached only when
    ///     the item's own category/dispatch field equals its numeric id, which is never one of the two
    ///     "stack-safe" category codes <c>IsStackItemSafe</c> checks; its <c>DecreaseQunatity</c> helper
    ///     therefore always takes the "zero the whole slot" branch for these items regardless of how many are
    ///     stacked, rather than decrementing by one the way <see cref="ConsumeAndMirrorAsync" />'s sibling
    ///     families do. Unlike the legacy call site (which discards the credit call's success/failure
    ///     indicator entirely, so a failed credit still silently reports success and still consumes the
    ///     item), this hardened path aborts before consuming the item or logging anything if the credit call
    ///     itself fails. There is deliberately no check that the slot's stored quantity is at least one
    ///     before crediting -- the legacy branch has none either, and none is invented here; see the
    ///     contract's Edge cases for why this is flagged, not assumed unreachable. The response never
    ///     reflects the credited amount in either value field, matching the legacy response's own inability
    ///     to observe it.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5369-5384 (the two case branches, the fixed 500/100
    ///     amounts, the two GL_606_USE_INVENTORY_ITEM calls, and the unconditional DecreaseQunatity call with
    ///     the credit call's return value discarded) ; Server/ts25zone/S04_MyWork03.cpp:593-611
    ///     (<c>IsStackItemSafe</c>) and :613-628 (<c>DecreaseQunatity</c>) -- the stack-safe category codes
    ///     and the "decrement by one" vs. "zero the whole slot" branching ; Server/ts25zone/H06_MyUpperCom.h:197
    ///     and Server/ts25zone/UpperCom/S06_MyUpperCom04.cpp:258-282 (the credit call's single-attempt, no-retry
    ///     semantics and its ignored return value) ; Server/ts25extra/S04_MyWork02.cpp:1077-1135 and
    ///     Server/ts25extra/S08_MyDB.cpp:114-135 (the receiving process's unbounded, unaudited credit --
    ///     deliberately NOT reproduced here: this path checks the credit outcome and writes a durable
    ///     game.EventLog audit row, neither of which the legacy receiving process does on this path).
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveGpTicketAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, ItemStack item, int creditAmount,
        CancellationToken cancellationToken)
    {
        try
        {
            await cash.CreditAsync(accountId, creditAmount, GpTicketCashCreditReason, item.ItemId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} GP ticket credit failed for item {ItemId} (character {CharacterId}); item left untouched",
                accountId, item.ItemId, characterId);
            return Fail(page, index);
        }

        await eventLog.LogAsync(GpTicketRedeemedEventCode, EventLogCategory.Currency, accountId, characterId,
            null, null, null, creditAmount, null, item.ItemId, item.Quantity, 1, null, cancellationToken);

        // Full-stack consumption, not decrement-by-one -- see this method's own <summary> for why.
        var projected = state.Inventory.GetContainer(page).Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item (GP ticket) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return response;
    }

    /// <summary>
    ///     Guild Scroll items (world.Items 558/1211/8415) recharge game.Guilds.BuffTime by a fixed per-item
    ///     amount -- <c>GuildActionHandler</c>'s tSort=14 buff-type choice can only ever be exercised once
    ///     this reserve is non-zero (see its own remarks: "only ever recharged by tSort 15's guild scrolls").
    ///     Requires guild membership, matching every scroll's own item text ("Need to join/be in a guild to
    ///     use the item"); a non-member or an already-vanished guild gets the same clean Result=1 as any
    ///     other rejected use, not a disconnect. BuffType/BuffState/BuffTimeForDiff are carried through
    ///     unchanged -- this only tops up the time reserve, it never itself activates/changes a buff type.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ResolveGuildScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        if (state.GuildId is not { } guildId)
            return Fail(page, index);

        var guild = await guilds.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
            return Fail(page, index);

        try
        {
            await guilds.SetBuffAsync(guildId, guild.BuffType, guild.BuffState, guild.BuffTime + minutes,
                guild.BuffTimeForDiff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild scroll recharge failed for guild {GuildId}", characterId, guildId);
            return Fail(page, index);
        }

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Faction Transfer Scroll items (world.Items 8153/8154, "Transfer to any Faction. Right click to
    ///     use.") -- only the permission gate is modeled here: consuming the scroll credits one banked
    ///     transfer permit (game.Characters.TribeTransferPermitCount) via
    ///     <see cref="ICharacterRepository.GrantTribeTransferPermitAsync" />. This is the mirror image of
    ///     game.Characters.BloodCoin (which has a spend path but no known grant path) -- a grant path with no
    ///     spend path yet, because no legacy source available here documents the actual tribe-change
    ///     mechanic that would consume it. No tribe mutation is invented.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ResolveTribeTransferScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        await characters.GrantTribeTransferPermitAsync(characterId, 1, cancellationToken);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Proxy-shop rental-extension consumables (world.Items 567/8422 grant one day, 592/8423 grant seven):
    ///     compounds onto the character's existing game.OfflineShops.ShopDate when it is still in the future,
    ///     otherwise extends from today. A character with no persisted proxy-shop record can still have this
    ///     succeed (see <see cref="IOfflineShopRepository.ExtendRentalAsync" />'s own remarks) -- nothing
    ///     observable distinguishes that case from an ordinary success. Fenrir keeps no in-memory mirror of
    ///     ShopDate on <c>PlayerRuntimeState</c> (see <c>PlayerRuntimeState.Misc</c>'s own PshopOpen/
    ///     PshopListing remarks: proxy-shop state deliberately lives only in game.OfflineShops so it keeps
    ///     working while the owning character is offline) -- the legacy's "advance an in-memory expiration
    ///     field" side effect therefore has no Fenrir equivalent to perform here. The legacy's other remaining
    ///     side effect (updating a live shared-registry entry in place) DOES have a Fenrir equivalent --
    ///     <see cref="Zone.TryUpdateProxyShopExpiration" /> against the zone hosting the shop's own broadcast
    ///     entry -- called best-effort, discarding whether it actually found an entry to update (see that
    ///     method's own remarks for why a miss is expected whenever the acting character isn't in the same
    ///     zone/shard as their shop). Stack consumption uses <see cref="CashItemStackConsumption" /> -- see
    ///     that type's own remarks for the unresolved stack-safe-category ambiguity for these four item ids.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2632-2675 (the full proxy-shop item branch: mapping,
    ///     date computation/failure check, cross-process persistence call/failure check, in-memory field
    ///     update, cash-item-use log call, quantity decrement, live-registry update) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:1855-1938 (opcode-wide preconditions -- cooldown, page/slot
    ///     bounds, last-page expiration, item-lookup failure -- already enforced by
    ///     <see cref="ResolveAsync" />/<c>UseInventoryItemHandler</c>, not re-checked here) ;
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom04.cpp:390-402 and Server/ts25extra/S04_MyWork02.cpp:1239-1253
    ///     and Server/ts25extra/S08_MyDB.cpp:1085-1106 (the cross-process persistence round trip, collapsed
    ///     here into a single <see cref="IOfflineShopRepository.ExtendRentalAsync" /> call whose thrown
    ///     exception collapses "unreachable" and "reported failure" into the same Result=1 response, matching
    ///     the legacy client's own inability to distinguish them) ; Server/Header/Protocol/DEFINE.h:309,369
    ///     (the four-faction, 500-slot-per-faction live shop registry that <see cref="Zone.TryUpdateProxyShopExpiration" />
    ///     is the Fenrir-sharded analogue of).
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveProxyShopRentalExtensionAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        var today = GameDate.Today();
        var (shop, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        var currentExpiration = shop?.ShopDate ?? 0;

        var resolved = ProxyShopRentalExtensionResolver.Resolve(item.ItemId, today, currentExpiration);
        if (resolved.Outcome != ProxyShopRentalExtensionResolver.Outcome.Success)
            return new UseInventoryItemResponse
                { Result = 1, Page = page, Index = index, Value = resolved.NewExpirationDate, Value2 = 0 };

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

        var remaining = CashItemStackConsumption.RemainingQuantity(item.ItemId, item.Quantity);
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

        // Best-effort live-registry mirror -- see this method's own <summary> for why a miss here is
        // expected and not logged as an error.
        zone.TryUpdateProxyShopExpiration(characterId, resolved.NewExpirationDate);

        return response;
    }

    /// <summary>
    ///     Item 1434 -- one of the two catalog ids the loot-box behavior contract's shared prologue handler
    ///     special-cases directly (the other, the rare mount box, is not wired to a production id yet -- see
    ///     <c>LootBoxRewardResolver</c>'s own remarks). Consumes exactly one unit on success, matching every
    ///     other family in this file's own default posture (the contract does not document an exception).
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2032-2117 (item 1434 direct handler).</remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveLodTicketAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = LodTicketResolver.Resolve(state.Level, state.RebirthCount, item.Quantity, state.LodRounds);
        if (!resolved.Succeeded)
            return Fail(page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, LodRounds: resolved.NewLodRounds), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped LOD-round mirror for character {CharacterId}",
                zone.MapId, characterId);

        var consumed = await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
        return consumed with { Value = resolved.NewLodRounds };
    }

    /// <summary>
    ///     Best-effort positional id-to-band mapping (five ids, four level bands, one "special" rebirth
    ///     variant folded onto the highest band) -- see <see cref="StatResetResolver" />'s own remarks for why
    ///     the exact assignment needs re-confirmation against the source before this ships to production.
    /// </summary>
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

    /// <summary>Same best-effort posture as <see cref="ResolveStatsClearBand" />, for the Stat Cleanse id set.</summary>
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

    /// <summary>
    ///     Stats Clear: full four-stat refund-and-floor, gated on the used id's level band matching the
    ///     character's actual level/rebirth state. Single-unit consumption, no bulk support (per contract).
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4249-4298.</remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveStatsClearAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, StatResetResolver.LevelBand requiredBand,
        CancellationToken cancellationToken)
    {
        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.RebirthCount, out var actualBand) ||
            actualBand != requiredBand)
            return Fail(page, index);

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

    /// <summary>
    ///     Stat Cleanse: single selected stat refund-and-floor. The target-selector range check (1-4) is a
    ///     disconnect-worthy malformed-input case per the originating contract; this port deliberately answers
    ///     with a clean failure instead of tearing down the session for it -- <see cref="IUseInventoryItemService" />'s
    ///     current return shape has no way to signal "please disconnect" back through this deep a call chain
    ///     without a broader refactor, and every other precondition in this whole file already collapses to a
    ///     clean failure, so this one narrow case is a documented, deliberate simplification rather than an
    ///     oversight -- flagged for a follow-up if byte-exact disconnect behavior is required here.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4301-4391.</remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveStatCleanseAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, StatResetResolver.LevelBand requiredBand, int selector,
        CancellationToken cancellationToken)
    {
        if (selector is < 1 or > 4)
            return Fail(page, index);

        if (!StatResetResolver.TryResolveLevelBand(state.Level, state.RebirthCount, out var actualBand) ||
            actualBand != requiredBand)
            return Fail(page, index);

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
            return Fail(page, index);

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
        int statInt, int statDex)
    {
        var attributes = new CharacterBaseAttributes(statVit, statStr, statInt, statDex, state.Level, state.Tribe,
            state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);
        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);
    }

    /// <summary>
    ///     Best-effort positional id-to-family mapping derived by matching the originating contract's own
    ///     prose ordering/counts against its coverage-note id groups one-for-one (Preserve(2)/Protection(4)/
    ///     Guardian(2)/AbsoluteCraft(2)/CPProtCharm(4+1) summed to exactly the expected 15 total) -- see
    ///     <see cref="ProtectionChargeResolver" />'s own remarks. Item 99405 is build-flag-gated in the source
    ///     (a specific online-service variant) -- wired here anyway since Fenrir has no build-variant concept,
    ///     flagged rather than omitted.
    /// </summary>
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

    /// <summary>
    ///     Preserve/Protection/Guardian/Absolute-Craft/CP-Prot Charm charge, bulk-aware. CP Prot Charm
    ///     (<see cref="ProtectionCharmCounterKind.Halo" />) additionally gates on the halo-rank threshold --
    ///     "halo rank" is modeled here as <see cref="PlayerRuntimeState.Halo" /> itself (the same field
    ///     StatCalculator already reads as "aHalo"), since no separate rank field was found in the cited range.
    /// </summary>
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
            return Fail(page, index);

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

        return new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = charged.NewCounterValue, Value2 = charged.UnitsConsumed
        };
    }

    /// <summary>
    ///     Same best-effort positional derivation posture as <see cref="ResolveCharmFamily" />. The 4th id in
    ///     each Combine/Upgrade/Drop group (1231/1232/1233) is a variant with no independently-confirmed charge
    ///     amount -- modeled here as matching its group's "L" tier amount, flagged rather than guessed finer.
    /// </summary>
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

    /// <summary>
    ///     Lucky Enchant/Combine/Upgrade/Drop Scroll charge: single-unit only, no bulk support, narrower
    ///     32-bit ceiling check -- see <see cref="ProtectionChargeResolver" />'s own remarks for why this
    ///     differs from the charm sub-group.
    /// </summary>
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
            return Fail(page, index);

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

    /// <summary>
    ///     Faction Notice Scroll (world.Item 566) -- recharges <see cref="PlayerRuntimeState.TribeNotifyScrollCount" />
    ///     by 5.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2622-2630.</remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveFactionNoticeAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = CashTimerResolver.ResolveFactionNotice(state.TribeNotifyScrollCount);
        if (!resolved.Succeeded)
            return Fail(page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: resolved.NewValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Faction-Notice mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Taiyan Key (world.Item 1049) -- <see cref="PlayerRuntimeState.TaiyanKeyTimer" />, gated on the level
    ///     cap. The level-cap-not-met precondition is disconnect-worthy per the originating contract; this port
    ///     answers with a clean failure instead, same documented simplification as
    ///     <see cref="ResolveStatCleanseAsync" />'s own remarks.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3712-3726.</remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveTaiyanKeyAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = CashTimerResolver.ResolveTaiyanKey(state.Level, state.TaiyanKeyTimer);
        if (!resolved.Succeeded)
            return Fail(page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, TaiyanKeyTimer: resolved.NewValue), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Taiyan-Key mirror for character {CharacterId}",
                zone.MapId, characterId);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Shared consume/persist/reply tail for every family beyond Bottle: decrements one unit off the used
    ///     stack (removing the slot outright once it hits zero, unlike the Bottle branch which always removes
    ///     the whole slot regardless of quantity), persists the container, and mirrors it onto this zone's own
    ///     cache. Result=0/Value=0/Value2=0 -- none of these families carry a documented per-family payload.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ConsumeAndMirrorAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
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

        return response;
    }

    /// <summary>
    ///     world.Items.Description1 states each scroll's minutes ("Accumulate Guild Scroll time for N
    ///     minutes.") but no numeric catalog column encodes it separately -- a code constant per item id here,
    ///     the same posture as <see cref="BottleResolver.RefillCount" />.
    /// </summary>
    private static int? GuildScrollBuffMinutes(int itemId)
    {
        return itemId switch
        {
            558 => 30,
            1211 or 8415 => 60,
            _ => null
        };
    }

    private static bool IsTribeTransferScroll(int itemId)
    {
        return itemId is 8153 or 8154;
    }

    private static UseInventoryItemResponse Fail(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

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
