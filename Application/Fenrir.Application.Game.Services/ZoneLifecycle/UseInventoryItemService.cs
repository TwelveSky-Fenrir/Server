using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
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

    /// <summary>world.Items.Sort for the "skill grimoire" category -- see <see cref="ResolveSkillGrimoireAsync" />.</summary>
    private const byte SkillGrimoireSort = 5;

    /// <summary>
    ///     game.EventLog.EventCode for the skill-grimoire "item consumed" audit entry -- scoped independently
    ///     within <see cref="EventLogCategory.ItemUse" /> (see <see cref="GpTicketRedeemedEventCode" />'s own
    ///     remarks on per-category scoping); distinct from <see cref="TeleportRecallScrollUsedEventCode" />'s
    ///     own 1 in the same category.
    /// </summary>
    private const short SkillGrimoireItemConsumedEventCode = 2;

    /// <summary>
    ///     game.EventLog.EventCode for the skill-grimoire "skill learned" audit entry, same category/scoping posture as
    ///     <see cref="SkillGrimoireItemConsumedEventCode" />.
    /// </summary>
    private const short SkillGrimoireSkillLearnedEventCode = 3;

    /// <summary>
    ///     game.EventLog.Outcome for both skill-grimoire audit entries -- 1 marks success, matching
    ///     <see cref="TeleportRecallScrollSuccessOutcome" />'s own precedent; this path has no failure branch left to log once
    ///     <see cref="SkillGrimoireLearnResolver" /> has already returned Success.
    /// </summary>
    private const byte SkillGrimoireLearnSuccessOutcome = 1;

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

    /// <summary>
    ///     game.EventLog.EventCode for a Pet EXP boost pill consumption (legacy <c>GL_605_USE_CASH_ITEM</c>),
    ///     scoped independently within <see cref="EventLogCategory.CashItemUse" /> -- distinct from
    ///     <see cref="ProxyShopRentalExtensionEventCode" />'s own 2 in the same category, same per-category
    ///     scoping posture as that constant's own remarks.
    /// </summary>
    private const short PetExpBoostPillUsedEventCode = 3;

    private const int LodTicketItemId = 1434;
    private const int FactionNoticeItemId = 566;
    private const int TaiyanKeyItemId = 1049;

    /// <summary>
    ///     game.EventLog.EventCode for the Teleport/Dungeon/Return Scroll "before" usage log -- an app-owned
    ///     numbering scheme scoped independently within <see cref="EventLogCategory.ItemUse" /> (see
    ///     <see cref="GpTicketRedeemedEventCode" />'s own remarks on per-category scoping).
    /// </summary>
    private const short TeleportRecallScrollUsedEventCode = 1;

    /// <summary>
    ///     game.EventLog.Outcome for <see cref="TeleportRecallScrollUsedEventCode" /> -- caller/EventCode-
    ///     defined (see game.EventLog.sql's own "Outcome is likewise a caller/EventCode-defined code"
    ///     comment). 1 marks success, matching
    ///     <see cref="Fenrir.Application.Game.Services.ItemModification.DestroyItemService" />'s own
    ///     precedent; this branch has no failure path to log once the (unmodeled here, see
    ///     <see cref="ResolveTeleportRecallScrollAsync" />'s own remarks) quantity-below-one precondition is
    ///     past.
    /// </summary>
    private const byte TeleportRecallScrollSuccessOutcome = 1;

    /// <summary>
    ///     game.EventLog.EventCode for the generic "item used" entry every successful stat/elixir-potion
    ///     consumption writes first (Server/ts25zone/S04_MyWork03.cpp:3157-3184's first, generic
    ///     GL_606_USE_INVENTORY_ITEM-style log call) -- distinct from <see cref="TeleportRecallScrollUsedEventCode" />'s
    ///     own 1 within the same <see cref="EventLogCategory.ItemUse" /> category (EventCode is only ever
    ///     caller-interpreted alongside its Category, so a second app-owned value here does not collide).
    /// </summary>
    private const short StatPotionUsedEventCode = 4;

    /// <summary>
    ///     game.EventLog.Outcome for both stat-potion audit entries below -- 1 marks success, same precedent as
    ///     <see cref="TeleportRecallScrollSuccessOutcome" />; this path has no failure branch left to log once
    ///     <see cref="StatPotionResolver" /> has already reported success.
    /// </summary>
    private const byte StatPotionSuccessOutcome = 1;

    /// <summary>
    ///     The three tribe-conversion book ids, excluded from <see cref="ResolveSkillGrimoireAsync" />'s
    ///     dispatch -- a distinct, separately-gated special case not modeled here (see
    ///     <see cref="SkillGrimoireLearnResolver" />'s own remarks). Left unhandled, they still fall through
    ///     to the generic <see cref="Fail" /> at the bottom of <see cref="ResolveAsync" />, same as before this
    ///     feature existed.
    /// </summary>
    private static readonly ImmutableHashSet<int> TribeConversionBookItemIds =
        ImmutableHashSet.Create(99014, 99015, 99016);

    public async ValueTask<UseInventoryItemResponse> ResolveAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, byte page, byte index, int value, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
            return Fail(page, index);

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

        if (IsTribeTransferScroll(item.ItemId))
            return await ResolveTribeTransferScrollAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        if (ProxyShopRentalExtensionResolver.ExtensionDaysFor(item.ItemId) is not null)
            return await ResolveProxyShopRentalExtensionAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        if (IsTeleportRecallScroll(item.ItemId))
            return await ResolveTeleportRecallScrollAsync(characterId, accountId, page, index, item, value,
                cancellationToken);

        if (ResolveStatPotionSpec(item.ItemId) is { } statPotionSpec)
            return await ResolveStatPotionAsync(zone, state, characterId, accountId, page, index, item,
                statPotionSpec.Kind, statPotionSpec.Tier, value, cancellationToken);

        if (IsPetExpBoostPill(item.ItemId))
            return await ResolvePetExpBoostPillAsync(zone, state, characterId, accountId, page, index, item, value,
                cancellationToken);

        return Fail(page, index);
    }

    /// <summary>
    ///     game.EventLog.EventCode for the stat-specific "which counter, which tier" entry every successful
    ///     stat/elixir-potion consumption writes second (Server/ts25zone/S04_MyWork03.cpp:3157-3184's second
    ///     log call, "twelve distinct sub-type codes... together with that stat's new counter value" per the
    ///     originating behavior contract) -- one code per (<see cref="StatPotionKind" />, ordinary-vs-g12)
    ///     pair, all independently scoped within <see cref="EventLogCategory.ItemUse" />.
    /// </summary>
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
    ///     Skill-grimoire consumption (world.Items iSort==5's general case, e.g. items 99011-99013 "Book of
    ///     Nobel Dragon/Royal Serpent/Grand Tiger" and the per-tribe 90101-90118 endgame grimoire family) --
    ///     learns the item's GainSkillNumber into the first empty slot of its category's sub-range. Gated on
    ///     the item's own tribe restriction (checked against the character's <b>previous</b> tribe, not its
    ///     current one -- same field <see cref="EquipItemValidationGate" /> reads for its own equip-time tribe
    ///     check), combined level, a duplicate-skill scan, the skill resolving in the catalog at all, and a
    ///     skill-point balance check -- see <see cref="SkillGrimoireLearnResolver" /> for the full rule and its
    ///     citations. The three tribe-conversion book ids (99014/99015/99016) never reach here; excluded at
    ///     the call site in <see cref="ResolveAsync" />.
    ///     <para>
    ///         Every failure branch in the legacy is a Quit() (disconnect); this port answers with a clean
    ///         Result=1 failure instead, the same documented simplification already used by this file's own
    ///         <see cref="ResolveStatCleanseAsync" />/<see cref="ResolveTaiyanKeyAsync" /> branches --
    ///         <see cref="IUseInventoryItemService" />'s return shape has no way to signal "please disconnect"
    ///         this deep without a broader refactor.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2347-2353 (success-path mutations: skill-point debit,
    ///     slot write, two audit-log calls, item-removal call) ; Server/ts25zone/S04_MyWork03.cpp:756-761
    ///     (the shared item-removal routine: clears the six inventory-slot fields and the three socket
    ///     fields, sends the success response, resets the slot's expiration-date field) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:91-104 (response-macro definitions, Result=0/1 semantics).
    /// </remarks>
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
            return Fail(page, index);

        var newSkillPoints = state.SkillPoints - resolved.Cost;
        var learned = new LearnedSkill(resolved.SkillId, resolved.Cost);

        await characters.UpsertSkillSlotAsync(characterId, resolved.Slot, resolved.SkillId, resolved.Cost,
            cancellationToken);

        if (!zone.PostSkillCommand(new SkillZoneCommand(characterId, resolved.Slot, learned, newSkillPoints)))
            logger.LogError(
                "Zone {MapId} skill inbox full: dropped skill-grimoire learn mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Item-slot-state-before-removal audit entry (S04_MyWork03.cpp:2347-2353's first log call) -- same
        // Serial/ExpireDate payload convention as this file's other item-use log calls.
        await eventLog.LogAsync(SkillGrimoireItemConsumedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, item.Quantity, SkillGrimoireLearnSuccessOutcome,
            $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

        // Learn-outcome audit entry (S04_MyWork03.cpp:2347-2353's second log call) -- ItemId still identifies
        // the consumed grimoire; none of the other named scalar columns (Quantity/DeltaMoney/DeltaBigMoney)
        // has an honest fit for "slot"/"learned skill id"/"point cost", so those three plus the post-debit
        // skill-point balance all go into Payload instead of stretching an unrelated column's semantics.
        await eventLog.LogAsync(SkillGrimoireSkillLearnedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, null, SkillGrimoireLearnSuccessOutcome,
            $"Slot={resolved.Slot};SkillId={resolved.SkillId};Cost={resolved.Cost};SkillPoints={newSkillPoints}",
            cancellationToken);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
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
    ///     use.") -- still only the permission gate is modeled here: consuming the scroll credits one banked
    ///     transfer permit (game.Characters.TribeTransferPermitCount) via
    ///     <see cref="ICharacterRepository.GrantTribeTransferPermitAsync" />. This remains a grant path with
    ///     no spend path, but the earlier premise for that gap ("no legacy source available here documents
    ///     the actual tribe-change mechanic") is now known to be false and must not be relied on going
    ///     forward: the full legacy mechanic (precondition chain, error messages, and the
    ///     costume/equipment/skill remap mutation) has since been located and is cited below. It is not yet
    ///     ported here because the legacy source itself has open gaps a wire-protocol-scoped change cannot
    ///     close: the equipment/costume equivalence tables' exact contents were not traced line-by-line (only
    ///     their shape was confirmed), the legacy "zone 37 reachability" and cross-process admin-toggle
    ///     checks (ts25extra's <c>skyinfo.s_tchange0/1/2</c>) have no Fenrir-side equivalent table/service
    ///     yet, and legacy's zone-relocation dispatch (<c>B_RETURN_TO_AUTO_ZONE</c>) has no settled mapping
    ///     onto Fenrir's map-sharded topology. Porting the actual spend/mutation is Domain+Services+Data work
    ///     (tribe-role/party/guild/mentor/cape/friend-list gates, the remap tables themselves, a new
    ///     admin-toggle equivalent, and a zone-relocation decision) that belongs with
    ///     <c>fenrir-gameplay-domain-engineer</c>/<c>fenrir-database-engineer</c>/
    ///     <c>fenrir-realtime-simulation-architect</c>, not invented here. No tribe mutation is invented.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4740-4856 (full precondition chain, error messages,
    ///     mutation call, item removal, proxy-shop closure, zone-routing dispatch) ;
    ///     Server/ts25zone/UpperCom/S06_MyUpperCom04.cpp:446-456 (the cross-process
    ///     U_ZONE_CHECK_CHANGE_TRIBE_FOR_EXTRA_SEND call shape) ; Server/ts25extra/S04_MyWork02.cpp:1322-1341
    ///     and Server/ts25extra/S08_MyDB.cpp:1217-1238 (the admin per-tribe transfer toggle,
    ///     <c>skyinfo.s_tchange&lt;N&gt;</c>) ; Server/ts25zone/S07_MyGame03.cpp:11525-11744
    ///     (<c>TChangeTribe</c>, the costume/equipment/skill/hotkey/auto-buff remap -- table contents
    ///     unverified, see this method's own summary) ; Server/Header/mapcheck.h:83-114 (<c>IsValidTown</c>) ;
    ///     Server/Header/function.h:92-114 (<c>ReturnTribeRole</c>) ; Server/Header/Protocol/DEFINE.h:483
    ///     (<c>LV_M33</c> = 145, the exact-equality level gate).
    /// </remarks>
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
    ///     Teleport Scroll / Dungeon Scroll / "return scroll" (world.Items 1109/1224/1026) -- per the
    ///     behavior contract for this family, using any of the three has no modeled effect beyond a durable
    ///     "before" usage log entry and an unconditional success reply: the legacy decrement/clear block is
    ///     compiled out in every build (gated on a flag defined unconditionally, never on a build variant),
    ///     so no quantity is consumed and the slot is left untouched here to match. There is no per-id
    ///     distinction between the three items in the cited legacy branch. The actual zone-change/teleport
    ///     mechanism this item is presumably meant to trigger is a separate, client-driven mechanism outside
    ///     this opcode's scope -- not modeled here, and not independently verified anywhere in this codebase
    ///     yet. The legacy quantity-below-one precondition (a disconnect there) is deliberately answered here
    ///     with a clean failure instead, the same documented simplification already used by
    ///     <see cref="ResolveStatCleanseAsync" />/<see cref="ResolveTaiyanKeyAsync" /> for their own
    ///     disconnect-worthy preconditions -- <see cref="IUseInventoryItemService" />'s return shape has no way
    ///     to signal "please disconnect" this deep without a broader refactor. <c>Value</c> passes the
    ///     request's own value field straight through unmodified, matching the legacy response helper;
    ///     <c>Value2</c> is documented in the legacy source as leftover shared dispatch state with no
    ///     meaningful per-request content (several unrelated branches assign it, this one never does) --
    ///     reproduced here as a fixed 0 rather than as a byte-for-byte artifact of legacy's single-threaded,
    ///     un-reset shared variable; flagged in the originating contract as a legacy state-sharing artifact,
    ///     not a deliberate protocol contract to reproduce.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2563 (switch(ttitem->iIndex) start) and :2599-2620
    ///     (case 1109/1224/1026 body, #ifdef WUSE_ITEM_1109) ; Server/Header/use_inventory.h:16 (#define
    ///     WUSE_ITEM_1109, unconditional -- confirms this case body is live, not dead code) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:2609-2615 (the decrement/clear block, gated on a flag defined
    ///     unconditionally at Server/Header/Protocol/DEFINE.h:18, and therefore dead in every build) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:100-103 (the response helpers sourcing Value from the request's
    ///     own value field and Value2 from shared, non-per-request state).
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolveTeleportRecallScrollAsync(int characterId,
        int accountId, byte page, byte index, ItemStack item, int value, CancellationToken cancellationToken)
    {
        if (item.Quantity < 1)
            return Fail(page, index);

        await eventLog.LogAsync(TeleportRecallScrollUsedEventCode, EventLogCategory.ItemUse, accountId, characterId,
            null, null, null, null, null, item.ItemId, item.Quantity, TeleportRecallScrollSuccessOutcome,
            $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

        // No inventory mutation and no zone command: the legacy decrement/clear block never runs in any
        // build (see this method's own remarks), and there is no other durable side effect to mirror.
        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = value, Value2 = 0 };
    }

    private static bool IsTeleportRecallScroll(int itemId)
    {
        return itemId is 1109 or 1224 or 1026;
    }

    /// <summary>
    ///     The 28-id table for the STR/DEX/VIT/MP/Elemental potion-and-elixir family (item-usage-consumables
    ///     finding, Critical). Every ordinary stat (Life/Mana/Str/Dex) has a 2-or-3-way "Potion"+"Elixir"(+
    ///     "bound" reskin) single-unit id set and a separate ten-unit-stack id set, plus one shared g12
    ///     high-tier id; the combined Elemental stat splits into an independent damage sub-family and defense
    ///     sub-family, each with its own single/ten/g12 triplet.
    /// </summary>
    /// <remarks>
    ///     Provenance, since the originating behavior contract deliberately gave the per-group tier <i>shape</i>
    ///     (single adds 1, ten-stack adds 10, g12 adds 1, in subvalue space) rather than a ready-made id table:
    ///     <list type="bullet">
    ///         <item>
    ///             The four Elemental ids (578/579/640/641) are exactly as cited by the contract's own
    ///             tAddTime/tAddTime2 pairs (578 1/1000 damage-only, 579 1/1 defense-only, 640 10/10000
    ///             damage-only, 641 10/10 defense-only -- 1000 being an exact multiple of
    ///             <see cref="ElementalPotionPacking.Modulus" /> is what makes a "damage-only" id possible at
    ///             all: adding a multiple of 1000 to the packed raw counter never perturbs the modulo-1000
    ///             defense remainder).
    ///         </item>
    ///         <item>
    ///             801-806 are the g12 tier verbatim per the finding's own text ("items 801-806, gated on
    ///             wAvatar.aLevel2 &gt;= MAX_LIMIT_HIGH_LEVEL_NUM=12"), and Fenrir's own already-imported
    ///             world.Items catalog text (Database/Migrations/Seed/world/080_items.sql) confirms their
    ///             per-id stat via flavor text ("Expanded VIT/INT/STR/Agility Elixir... Increase [20] HP/
    ///             [25] MP/[3] damage/[2] AR,Dodge permanently", "Expand A Damage/Defense Elixir... Increase
    ///             E. damage/defense by 10 permanently").
    ///         </item>
    ///         <item>
    ///             The remaining 18 ordinary-tier ids (506-509, 636-639, 1017/1018, 1092/1093, 17026-17029,
    ///             17038/17039) are cross-referenced the same way against that same catalog text -- e.g. 506
    ///             "VIT Elixir... Increase [20] HP", 1017 "Vitality Elixir... Increase [20] HP", and 17038
    ///             "(bound variant, same [20] HP text)" all independently resolve to Life/single, while 636
    ///             "VIT Elixir(M)... 10ea. at once" and 17026 "(bound ten-unit reskin)" resolve to Life/
    ///             ten-stack; the doubled/tripled "Potion"+"Elixir"+"bound" id sets per stat this uncovers are
    ///             exactly what this type's own predecessor remarks (before this finding was wired) flagged as
    ///             not yet reconstructed with confidence. This id-to-catalog-text cross-reference was NOT
    ///             independently re-verified against the raw <c>switch(ttitem-&gt;iIndex)</c> statement itself
    ///             (Server/ts25zone/S04_MyWork03.cpp:2879-2947) the way the Elemental/g12 groups above were --
    ///             flagged for a cpp-protocol-header-analyst re-check if a byte-exact per-id guarantee is
    ///             ever required beyond what the catalog text already corroborates.
    ///         </item>
    ///     </list>
    /// </remarks>
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

    /// <summary>
    ///     Reads the sub-value <paramref name="kind" /> currently occupies -- unpacking
    ///     <see cref="PlayerRuntimeState.EatElePotion" /> for the two Elemental kinds.
    /// </summary>
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

    /// <summary>
    ///     Computes the new RAW counter value to persist for <paramref name="kind" /> given a freshly-resolved
    ///     sub-value -- for the four non-Elemental kinds this is the sub-value itself; for the two Elemental
    ///     kinds this repacks it against <paramref name="state" />'s CURRENT <see cref="PlayerRuntimeState.EatElePotion" />
    ///     so the other, untouched sub-value survives.
    /// </summary>
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

    /// <summary>
    ///     Stat/elixir-potion consumption (item-usage-consumables finding, Critical): the highest-impact gap
    ///     this finding closed -- every one of the 28 recognized ids previously fell through to the generic
    ///     <see cref="Fail" /> with no stack decrement and no stat/HP/MP change whatsoever, despite
    ///     <see cref="StatPotionResolver" /> already correctly modeling the 200/400 cap math. Bulk-aware for
    ///     every tier (including g12, which the resolver's own predecessor incorrectly modeled as
    ///     single-unit-only -- see <see cref="StatPotionResolver.ResolveG12" />'s own remarks for the fix).
    ///     <c>Value</c> deliberately echoes the client's own raw requested count unmodified on both success and
    ///     failure -- the true new counter value is only ever observable via the post-consumption avatar-action
    ///     refresh, matching the legacy response helper's own behavior for this specific branch (unlike several
    ///     sibling branches elsewhere in this file that DO overwrite <c>Value</c> with an authoritative result).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2900-3211 (the whole switch) ; :2879-2914 (outer entry
    ///     dispatch, tAddTime initialized to 1) ; :2915-2947 (inner tier-amount switch) ; :2948 (GetBulkStackUseCount
    ///     call -- <see cref="BulkUseCoercion.Coerce" /> is this codebase's own transcription of that helper,
    ///     already flagged in its own remarks as reused by this exact family) ; :2951-3156 (per-id assignment
    ///     switch: counter selection, headroom formula, clamp-to-headroom, g12 preconditions/math) ;
    ///     Server/Header/Protocol/DEFINE.h:361-362 (200/400 caps) ; :605 (12, g12 level gate) ;
    ///     Server/ts25zone/S07_MyGame03.cpp:9132-9139 (Elemental packed-dual-value read) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:3157-3184 (post-switch stat/HP-MP recompute, the generic-plus-
    ///     stat-specific audit-log pair) ; :3186 (DecreaseQunatityEx, the clamped bulk count) ; :3188-3211
    ///     (post-consumption double avatar-state refresh to self plus AOI-neighbor broadcast -- see
    ///     <see cref="TribeProgressZoneCommand.FullActionRebroadcast" />'s own remarks for why Fenrir sends the
    ///     self-refresh once, not literally twice).
    /// </remarks>
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
                return Fail(page, index);

            newSubValue = g12.NewCount;
            unitsConsumed = g12.UnitsConsumed;
        }
        else
        {
            var perUnitAmount = tier == StatPotionTier.TenStack ? 10 : 1;
            var ordinary = StatPotionResolver.ResolveOrdinary(currentSubValue, perUnitAmount, bulkRequested);
            if (!ordinary.Succeeded)
                return Fail(page, index);

            newSubValue = ordinary.NewCount;
            unitsConsumed = ordinary.UnitsConsumed;
        }

        var newRawCounter = ResolvedStatPotionRawCounter(state, kind, newSubValue);

        // General equipment/buff-driven recompute -- not potion-specific logic, the same routine
        // ResolveStatsClearAsync/ResolveStatCleanseAsync above already trigger for their own, unrelated,
        // stat-affecting branches. None of the five Eat*Potion counters feed this computation today (no
        // StatCalculator formula consumes them yet -- a separate, not-yet-scheduled gap), so this call is a
        // no-op in practice until that formula exists; kept here anyway to match the legacy side-effect list
        // and stay correct automatically the day that formula lands.
        var updatedStats = RecomputeStatsAfterReset(state, state.StatVit, state.StatStr, state.StatInt,
            state.StatDex);

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

        // Generic "item used" entry, then the stat-specific entry carrying the new counter value in Payload
        // (no named scalar column has an honest fit for "the resulting sub-value of a packed counter") --
        // same two-entries-per-success shape as ResolveSkillGrimoireAsync above.
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

        // Value echoes the client's own raw requested count unmodified -- see this method's own <summary>.
        return new UseInventoryItemResponse
            { Result = 0, Page = page, Index = index, Value = requestedValue, Value2 = 0 };
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
            state.PreviousTribe, state.Title, state.Halo, state.RebirthCount);
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
    ///     Pet EXP boost pill (world.Items 1190/17035/8413, legacy <c>aPetExpX2Time</c>) -- bulk-aware charge
    ///     onto <see cref="PlayerRuntimeState.PetExpX2Time" />, 180 units per unit consumed (charge math and
    ///     the overflow-reject path both live in <see cref="PetExpBoostPillResolver" />). Once positive, this
    ///     counter doubles pet-kill EXP (<see cref="Zone.CreditPetGrowthFromMonsterKill" />) and pauses
    ///     pet-activity decay (<see cref="Fenrir.Application.Game.Domain.Simulation.PetActivitySystem" />)
    ///     until <see cref="Fenrir.Application.Game.Domain.Simulation.PetExpBoostCountdownSystem" /> counts it
    ///     back down -- neither of those two periodic effects is triggered from here; this method only grants
    ///     the one-shot charge and writes its audit trail. Item id 99402 (a fourth trigger id in the same
    ///     legacy case block, gated behind the non-production <c>ONLINE_FOR_DS</c> branch) is deliberately not
    ///     wired into <see cref="IsPetExpBoostPill" /> -- see <see cref="PetExpBoostPillResolver" />'s own
    ///     remarks.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5440-5468 (case 1190/17035/8413 -- the 180-unit charge,
    ///     bulk multiplication, and the overflow-reject path, already covered in full by
    ///     <see cref="PetExpBoostPillResolver" />'s own remarks) ; Server/ts25zone/S04_MyWork03.cpp:629-669
    ///     (<c>GetBulkStackUseCount</c>, modeled here -- as in every sibling bulk-aware branch in this file --
    ///     by <see cref="BulkUseCoercion.Coerce" />) ; Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:320-325
    ///     and Server/ts25zone/H06_MyUpperCom.h:271 (<c>GL_605_USE_CASH_ITEM</c> -- user index, item id, the
    ///     RESULTING (post-decrement) quantity, an item "value" field, and a "recognition number" field ;
    ///     modeled here as <see cref="ItemValueCodec.Encode" /> for "value" and <see cref="ItemStack.Serial" />
    ///     for "recognition number" -- the same two-field mapping this codebase already uses everywhere else a
    ///     legacy item's packed-upgrade/serial pair needs a C# representation, e.g.
    ///     <see cref="Fenrir.Application.Game.Domain.Inventory.InventoryToWorldDropPolicy" />) ;
    ///     Server/ts25zone/S04_MyWork03.cpp:613-628 (<c>DecreaseQunatity</c>) and :671-711
    ///     (<c>DecreaseQunatityEx</c>) -- stack decrement and slot-clear-on-empty (including the
    ///     expiration-date reset that removing the slot's <see cref="ItemStack" /> entry outright already
    ///     performs here, same as every sibling branch's own slot-clear path in this file).
    /// </remarks>
    private async ValueTask<UseInventoryItemResponse> ResolvePetExpBoostPillAsync(Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, byte page, byte index, ItemStack item,
        int requestedValue, CancellationToken cancellationToken)
    {
        var bulkCount = BulkUseCoercion.Coerce(requestedValue, item.Quantity);

        var charged = PetExpBoostPillResolver.ResolveCharge(state.PetExpX2Time, bulkCount);
        if (!charged.Succeeded)
            return Fail(page, index);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, PetExpX2Time: charged.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Pet-EXP-boost-pill mirror for character {CharacterId}",
                zone.MapId, characterId);

        // "The resulting stack quantity" per this method's own citation -- the post-decrement remainder, not
        // the pre-use stack size (unlike ResolveGpTicketAsync/ResolveProxyShopRentalExtensionAsync's own log
        // calls above, which log the pre-decrement quantity for their own, differently-cited, item families).
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

        // Value carries the resulting counter total, Value2 the bulk count actually consumed -- per this
        // family's own Outputs contract, distinct from ResolveProtectionCharmAsync's identically-shaped
        // response (same two-field convention, different source counter).
        return new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = charged.NewCounterValue, Value2 = bulkCount
        };
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

    /// <summary>world.Items 1190/17035/8413 -- see <see cref="ResolvePetExpBoostPillAsync" />.</summary>
    private static bool IsPetExpBoostPill(int itemId)
    {
        return itemId is 1190 or 17035 or 8413;
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

    /// <summary>
    ///     Which of the five banked counters a recognized stat/elixir-potion id feeds -- see
    ///     <see cref="ResolveStatPotionSpec" />.
    /// </summary>
    private enum StatPotionKind
    {
        Life,
        Mana,
        Str,
        Dex,
        ElementalDamage,
        ElementalDefense
    }

    /// <summary>
    ///     Which of the three consumption tiers a recognized stat/elixir-potion id belongs to -- see
    ///     <see cref="ResolveStatPotionSpec" />.
    /// </summary>
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
