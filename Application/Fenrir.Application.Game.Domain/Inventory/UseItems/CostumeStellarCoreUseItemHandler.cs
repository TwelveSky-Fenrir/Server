using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23's costume/stellar-core intermediate dispatch block (workstream C9-costume-stellar-whitelist): the
///     terminal fallback that claims any item id in <see cref="CostumeStellarCoreWhitelist.IsValidCostume" />
///     or <see cref="CostumeStellarCoreWhitelist.IsValidStellarCore" />, once no id-keyed
///     <see cref="UseItemHandlerRegistry" /> entry and no <see cref="EquipSwapUseItemHandler" /> category match
///     already claimed it. Reached only for a used item whose category value fell outside the entire
///     preceding op23 category dispatch (Server/ts25zone/S04_MyWork03.cpp:2119-2486) -- see the originating
///     contract's own Preconditions for the exact excluded-category set.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2488-2525 (<c>IsValidCostume</c> branch: duplicate check,
///     free-slot search + disconnect-on-full, two audit-log entries, array/date/expire-date writes, then the
///     shared <c>RemoveItem</c> consume-and-acknowledge) and :2526-2561 (<c>IsValidStellarCore</c> branch,
///     structurally identical, unguarded by any build macro unlike the costume branch's own
///     <c>WUSE_ITEM_FASHION</c> wrapper).
///     <para>
///         <b>Check order is fixed, matching legacy</b>: costume is always tried before stellar-core
///         (S04_MyWork03.cpp:2489 then :2528) -- moot for routing today since the two tables are confirmed
///         disjoint, but preserved anyway so a future id collision would resolve the same way legacy does.
///     </para>
///     <para>
///         <b>"No free slot" is a genuine disconnect, not softened to Result=1</b> -- unlike most other
///         disconnect-worthy op23 preconditions this file family collapses to a clean failure reply (see
///         <see cref="UseItemHandlerRegistry" />'s own remarks on that established simplification), the
///         originating contract's own Error/failure semantics section is explicit that this one is a real,
///         distinct legacy <c>Quit()</c> with no acknowledgment sent at all, asymmetric against the sibling
///         "already worn" rejection (a normal Result=1 reply). <see cref="ClientSession.Send{TPacket}" />
///         silently no-ops once <see cref="ClientSession.Abort" /> has already set the session's completed
///         flag (verified: <c>Fenrir.Network.Dispatch.Sessions.ClientSession.Send{TPacket}</c>'s own early
///         `Volatile.Read(ref _completed) != 0` guard) -- so calling <c>Abort</c> here and still returning a
///         (never-transmitted) response to satisfy <see cref="IUseItemHandler" />'s signature reproduces "no
///         acknowledgment sent at all" exactly, without widening this interface's return shape (which would
///         ripple into every sibling handler, including the C10 loot-box family this workstream must not
///         touch). This is the same in-tick, no-coded-response hard-disconnect idiom
///         <see cref="Simulation.PersonalShopRegionEnforcementSystem" />/<see cref="Simulation.DeathGateTickSystem" />
///         already use elsewhere in this project (<c>state.Session is ClientSession client</c> then
///         <c>client.Abort(reason)</c>), applied here from a request-thread handler instead of a tick-thread
///         system.
///     </para>
///     <para>
///         <b>"Costume date" is translated as the granting item's packed enchant/combine/refine/socket word</b>,
///         not literally named in the zero-code contract ("copied from one of the source item's own auxiliary
///         attribute fields"). This mirrors the established <see cref="World.PlayerRuntimeState.RuneSystemStat" />
///         convention (paired 1:1 with <see cref="World.PlayerRuntimeState.RuneSystem" />, same packed-word
///         shape) and is exactly what <see cref="Stats.StatCalculator.DecodeCostumeEnchantCs" />'s own
///         "ten packed slot words, low byte read as signed" consumption contract expects -- a well-corroborated
///         translation choice, not an invented legacy fact.
///     </para>
///     <para>
///         <b>No repository persistence for the wardrobe/date/expire-date arrays themselves</b> -- none exists
///         anywhere in this schema (<c>ICharacterRepository</c>'s own "Costume/CostumeIndex... no persisted
///         storage exists anywhere in this schema yet" remarks), so the grant mirrors into
///         <see cref="World.PlayerRuntimeState" /> only, via <see cref="World.Zone.PostCostumeCommand" />/
///         <see cref="World.Zone.PostStellarCoreCommand" /> (fire-and-forget, matching every other
///         Costume/StellarCore mutation in this codebase -- neither has an <c>...AndWaitAsync</c> wrapper).
///         The source inventory slot's consumption IS durable (SQL, via <see cref="UseItemInventoryWriter" />)
///         and is applied BEFORE the in-memory grant is posted -- the same "consume first" hardening
///         <see cref="TitleUpgradeUseItemHandler" /> documents on itself ("a dropped mirror can only lose the
///         [grant], never duplicate the item").
///     </para>
/// </remarks>
public sealed class CostumeStellarCoreUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<CostumeStellarCoreUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>
    ///     game.EventLog.EventCode for the "source slot's pre-consumption contents" audit entry -- same
    ///     two-call shape as <see cref="TitleUpgradeUseItemHandler" />'s own
    ///     <c>TitleUpgradeSpendEventCode</c>/its sibling, scoped independently within
    ///     <see cref="EventLogCategory.ItemUse" />.
    /// </summary>
    private const short WardrobeItemConsumedEventCode = 1;

    /// <summary>
    ///     game.EventLog.EventCode for the "destination wardrobe slot + granted item id" audit entry, same
    ///     category as <see cref="WardrobeItemConsumedEventCode" />.
    /// </summary>
    private const short WardrobeGrantedEventCode = 2;

    private const byte SuccessOutcome = 1;

    /// <summary>Registry routing predicate -- see <see cref="CostumeStellarCoreWhitelist.ClaimsItem" />.</summary>
    public static bool ClaimsItem(int itemId)
    {
        return CostumeStellarCoreWhitelist.ClaimsItem(itemId);
    }

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var itemId = context.Item.ItemId;

        // Costume always tried before stellar-core, matching the legacy call-site order -- see this class's
        // own remarks. The registry only resolves this handler when one of the two whitelists already
        // matched (ClaimsItem), so the trailing "neither" branch below is a defensive-only fallback.
        if (CostumeStellarCoreWhitelist.IsValidCostume(itemId))
            return await HandleCostumeAsync(context, cancellationToken);

        if (CostumeStellarCoreWhitelist.IsValidStellarCore(itemId))
            return await HandleStellarCoreAsync(context, cancellationToken);

        logger.LogWarning(
            "Character {CharacterId} op23 costume/stellar-core dispatch reached for item {ItemId}, which matches neither whitelist -- registry routing bug",
            context.CharacterId, itemId);
        return UseItemResponses.Fail(context.Page, context.Index);
    }

    private async ValueTask<UseInventoryItemResponse> HandleCostumeAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var item = context.Item;

        var resolved = CostumeStellarCoreGrantResolver.Resolve(state.CostumeWardrobe, item.ItemId);

        switch (resolved.Outcome)
        {
            case CostumeStellarCoreGrantResolver.Outcome.AlreadyWorn:
                logger.LogDebug(
                    "Character {CharacterId} op23 costume grant rejected: item {ItemId} already worn",
                    context.CharacterId, item.ItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.NoFreeSlot:
                logger.LogInformation(
                    "Character {CharacterId} op23 costume grant: wardrobe full for item {ItemId} -- disconnecting (legacy Quit(), no acknowledgment)",
                    context.CharacterId, item.ItemId);
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.WardrobeFull);
                // Never actually transmitted: ClientSession.Send<TPacket> no-ops once Abort has run -- see
                // this class's own remarks.
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.Success:
            {
                var slot = resolved.SlotIndex;
                var packedCostumeDate = ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket);

                await eventLog.LogAsync(WardrobeItemConsumedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, item.Quantity, SuccessOutcome,
                    $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

                await eventLog.LogAsync(WardrobeGrantedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, null, SuccessOutcome,
                    $"WardrobeSlot={slot}", cancellationToken);

                // Consume the source slot first (durable) -- see this class's own remarks for why this
                // ordering matters even though the grant mirror itself has no SQL backing yet.
                await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
                    context.Index, item, 0, null, cancellationToken);

                if (!context.Zone.PostCostumeCommand(new CostumeZoneCommand(context.CharacterId,
                        WardrobeSlotGranted: slot, GrantedItemId: item.ItemId, GrantedCostumeDate: packedCostumeDate,
                        GrantedExpireDate: item.ExpireDate)))
                    logger.LogError(
                        "Zone {MapId} costume inbox full: dropped op23 costume-grant mirror for character {CharacterId} -- item already durably consumed, wardrobe slot {Slot} will NOT reflect the grant until a future costume mutation resynchronizes it",
                        context.Zone.MapId, context.CharacterId, slot);

                logger.LogInformation(
                    "Character {CharacterId} op23 costume grant applied: item {ItemId} -> wardrobe slot {Slot}",
                    context.CharacterId, item.ItemId, slot);

                return UseItemResponses.Success(context.Page, context.Index, slot);
            }

            default:
                return UseItemResponses.Fail(context.Page, context.Index);
        }
    }

    private async ValueTask<UseInventoryItemResponse> HandleStellarCoreAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var item = context.Item;

        var resolved = CostumeStellarCoreGrantResolver.Resolve(state.StellarCoreWardrobe, item.ItemId);

        switch (resolved.Outcome)
        {
            case CostumeStellarCoreGrantResolver.Outcome.AlreadyWorn:
                logger.LogDebug(
                    "Character {CharacterId} op23 stellar-core grant rejected: item {ItemId} already worn",
                    context.CharacterId, item.ItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.NoFreeSlot:
                logger.LogInformation(
                    "Character {CharacterId} op23 stellar-core grant: wardrobe full for item {ItemId} -- disconnecting (legacy Quit(), no acknowledgment)",
                    context.CharacterId, item.ItemId);
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.WardrobeFull);
                return UseItemResponses.Fail(context.Page, context.Index);

            case CostumeStellarCoreGrantResolver.Outcome.Success:
            {
                var slot = resolved.SlotIndex;

                await eventLog.LogAsync(WardrobeItemConsumedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, item.Quantity, SuccessOutcome,
                    $"Serial={item.Serial};ExpireDate={item.ExpireDate}", cancellationToken);

                await eventLog.LogAsync(WardrobeGrantedEventCode, EventLogCategory.ItemUse, context.AccountId,
                    context.CharacterId, null, null, null, null, null, item.ItemId, null, SuccessOutcome,
                    $"WardrobeSlot={slot}", cancellationToken);

                await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
                    context.Index, item, 0, null, cancellationToken);

                if (!context.Zone.PostStellarCoreCommand(new StellarCoreZoneCommand(context.CharacterId,
                        WardrobeSlotGranted: slot, GrantedItemId: item.ItemId, GrantedExpireDate: item.ExpireDate)))
                    logger.LogError(
                        "Zone {MapId} stellar-core inbox full: dropped op23 stellar-core-grant mirror for character {CharacterId} -- item already durably consumed, wardrobe slot {Slot} will NOT reflect the grant until a future stellar-core mutation resynchronizes it",
                        context.Zone.MapId, context.CharacterId, slot);

                logger.LogInformation(
                    "Character {CharacterId} op23 stellar-core grant applied: item {ItemId} -> wardrobe slot {Slot}",
                    context.CharacterId, item.ItemId, slot);

                return UseItemResponses.Success(context.Page, context.Index, slot);
            }

            default:
                return UseItemResponses.Fail(context.Page, context.Index);
        }
    }
}
