using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmCreateItemService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1036-1095 (case 505/523 body: privilege gate, id-range/catalog
///     checks, quantity resolution, stackable-vs-unique branches) ; Server/ts25zone/S04_MyWork04.cpp:1068-1077
///     (stackable branch's own <c>tResult=2; break;</c>, which exits the outer <c>switch(tSort)</c> directly
///     and so preserves the failure code -- see <see cref="StackableQuantityRejectedResult" />) ;
///     Server/ts25zone/S04_MyWork04.cpp:1078-1093 (non-stackable branch's identical-looking
///     <c>tResult=2; break;</c>, which instead sits inside a <c>for</c> loop and so only exits that loop,
///     falling through to the unconditional <c>tResult=0;</c> overwrite at :1093 -- the only branch where the
///     "always accepted" masking genuinely occurs) ; Server/Header/Protocol/DEFINE.h:73 (<c>USE_MATS_999</c>
///     unconditionally defined, keeping the iSort==99 stackable case live in every build) ;
///     Server/ts25zone/S04_MyWork04.cpp:7 (<c>eLocation</c> macro resolving to the acting user's
///     own live location -- mirrored here by always spawning at <c>state.Pos*</c>, the GM's own
///     <see cref="PlayerRuntimeState" />, never any other target) ; Server/ts25zone/S07_MyGame03.cpp:505-527
///     (stackable-item quantity default-to-cap-on-zero and 1-999 bound enforcement) ;
///     Server/ts25zone/S07_MyGame03.cpp:578-594 (world item-object pool slot search/exhaustion -- see this
///     type's own remarks for why that failure path is unreachable in Fenrir's own reimplementation) ;
///     Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:289-305 and Server/ts25zone/H06_MyUpperCom.h:268
///     (audit-log call for item creation -- see this type's own remarks for the inexact field mapping) ;
///     Server/Header/Protocol/DEFINE.h:611 (MAX_ITEM_DUPLICATION_NUM = 999, ported as
///     <see cref="GroundItemPickupPolicy.MaxStackQuantity" />) ; Server/Header/Protocol/DEFINE.h:520
///     (GM-drop-source code = 13, ported as <see cref="GroundItemEntity.GmCreateItemDropSort" />) ;
///     Server/ts25zone/S07_MyGame03.cpp:650 (this drop source excluded from the cluster-wide "elite item
///     drop" inter-zone announcement list -- moot in Fenrir today since no such announcement subsystem is
///     implemented for any drop source yet, so nothing here can accidentally include it).
/// </summary>
/// <remarks>
///     Legacy's world item-object pool is a fixed-size array whose exhaustion is a real, observable failure
///     mode that, in the non-stackable per-unit branch only, legacy's own "always report accepted" control-flow
///     defect masks from the client (see <see cref="AcceptedResult" />/<see cref="StackableQuantityRejectedResult" />'s
///     own remarks for why the stackable branch does not share that masking). Fenrir's own
///     <see cref="Zone.SpawnGroundItem" /> (reached here via <see cref="TribeProgressZoneCommand.DropItems" />)
///     is backed by an unbounded <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />
///     with no capacity ceiling, so that specific pool-exhaustion failure mode cannot occur in this
///     implementation at all -- every creation this method decides to attempt (after the id-range/catalog and,
///     for a stackable item, the 1-999 quantity-bound checks) unconditionally succeeds once attempted. This is
///     a genuine, documented architectural divergence from legacy, not a bug: pool exhaustion specifically is
///     unobservable either way, since Fenrir has no equivalent fixed-size pool to exhaust.
///     <para>
///         The audit-log call's exact legacy argument list ("refine/combine/improve metadata associated with
///         that item id") was not independently re-derived from
///         Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:289-305/H06_MyUpperCom.h:268 for this change --
///         <see cref="IEventLogRepository.LogAsync" /> has no dedicated refine/combine/improve parameters, so
///         this method packs the closest available catalog-level fields
///         (<see cref="Fenrir.Data.Abstractions.World.ItemRowDto.CheckImprove" />/
///         <see cref="Fenrir.Data.Abstractions.World.ItemRowDto.CheckHighImprove" />/
///         <see cref="Fenrir.Data.Abstractions.World.ItemRowDto.CheckSetItem" />) into the free-text
///         <c>payload</c> column as a best-effort inference, not an asserted byte-exact citation. Flag for a
///         follow-up <c>cpp-zone-gameplay-analyst</c> citation if exact parity with that call's own argument
///         list matters later. The audit row's own "value" field is separately confirmed fixed at zero by the
///         source contract -- <see cref="GroundItemEntity.Value" /> is already always 0 for every
///         <see cref="Zone.SpawnGroundItem" /> caller, and the payload string also states it explicitly for
///         clarity.
///     </para>
///     <para>
///         Legacy assigns each created object a freshly issued unique serial number; Fenrir's own
///         <see cref="Zone.SpawnGroundItem" /> does not implement serial-number issuance for any caller yet
///         (every ground item -- monster drops, manual drops, and now GM-created items alike -- carries
///         <see cref="GroundItemEntity.SerialNumber" /> = 0). This is a pre-existing Fenrir-wide gap this
///         command inherits rather than introduces, and is out of scope to fix here: changing
///         <see cref="Zone.SpawnGroundItem" />'s serial-number handling would affect every other drop path
///         too, not just this one.
///     </para>
/// </remarks>
public sealed class GmCreateItemService(
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<GmCreateItemService> logger) : IGmCreateItemService
{
    /// <summary>
    ///     Legacy tResult's own default-initialized/rejected value (same convention as GmBlockAvatarService's
    ///     ResultTargetNotFound).
    /// </summary>
    private const int RejectedResult = 1;

    /// <summary>
    ///     Forced unconditionally once the id-range/catalog gate passes for the non-stackable per-unit branch,
    ///     regardless of whether creation actually happened there -- see this type's own remarks and the
    ///     source contract's "Error/failure semantics" section for why that masking is preserved rather than
    ///     "fixed." Also used for the stackable branch whenever it actually creates at least one object.
    /// </summary>
    private const int AcceptedResult = 0;

    /// <summary>
    ///     Legacy's own <c>tResult=2</c> for the stackable-item branch (Server/ts25zone/S04_MyWork04.cpp:1068-1077)
    ///     when the resolved quantity is out of range: that <c>break;</c> sits directly inside the outer
    ///     <c>switch(tSort)</c> (not inside any loop), so it exits the switch immediately and preserves the
    ///     failure code on the wire -- unlike the non-stackable branch's identical-looking <c>tResult=2; break;</c>
    ///     at :1078-1093, which sits inside a <c>for</c> loop and only exits that loop, falling through to the
    ///     unconditional <c>tResult=0;</c> at :1093. Only the stackable branch (tSort 523, iSort 2/99) reports
    ///     this failure code; the non-stackable branch keeps reporting <see cref="AcceptedResult" /> even when it
    ///     creates nothing, matching legacy's own control-flow defect there.
    /// </summary>
    private const int StackableQuantityRejectedResult = 2;

    private const int MinItemId = 2;
    private const int MaxItemId = 99999;

    /// <summary>
    ///     game.EventLog.EventCode for this category's first (and, today, only) app-owned variant -- see
    ///     <see cref="EventLogCategory.ItemCreate" />'s own remarks (a reserved-but-previously-unused category
    ///     this command is the first real consumer of).
    /// </summary>
    private const short ItemCreateEventCode = 1;

    private const byte ItemCreateOutcome = 1;

    public async ValueTask HandleAsync(int sort, byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier spawn-item command (sort {Sort}) without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId, sort);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        int itemId;
        int requestedQuantity;
        if (sort == 523)
        {
            if (!GmCreateItemQuantityPayload.TryRead(data, out var quantityPayload))
            {
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            }

            itemId = quantityPayload.ItemId;
            requestedQuantity = quantityPayload.Quantity;
        }
        else
        {
            if (!GmCreateItemPayload.TryRead(data, out var basePayload))
            {
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            }

            itemId = basePayload.ItemId;
            requestedQuantity = 0; // sort 505 never reads a quantity field -- always treated as unspecified.
        }

        if (itemId is < MinItemId or > MaxItemId || !worldData.ItemsById.TryGetValue(itemId, out var definition))
        {
            logger.LogInformation(
                "Character {CharacterId} spawn-item rejected: item {ItemId} out of range or not in the catalog (sort {Sort})",
                zoneSession.CharacterId, itemId, sort);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = sort, Data = data, RuneValue = 0 });
            return;
        }

        var isStackable = ContainerMatrix.IsStackableSort(definition.Item.Sort);
        var drops = ResolveDrops(sort, itemId, requestedQuantity, definition.Item.Sort);

        if (drops.Count > 0 &&
            !await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, DropItems: [..drops]), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped spawn-item mirror for character {CharacterId} (item {ItemId}) -- reporting accepted regardless, matching legacy's own result-code masking",
                zone.MapId, state.CharacterId, itemId);

        foreach (var drop in drops)
            await eventLog.LogAsync(ItemCreateEventCode, EventLogCategory.ItemCreate, zoneSession.AccountId,
                zoneSession.CharacterId, null, null, null, null, null, drop.ItemId, drop.Quantity, ItemCreateOutcome,
                $"GmName={state.Name};Value=0;CheckImprove={definition.Item.CheckImprove};CheckHighImprove={definition.Item.CheckHighImprove};CheckSetItem={definition.Item.CheckSetItem}",
                cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} spawn-item applied: item {ItemId}, {ObjectCount} object(s) created (sort {Sort})",
            state.CharacterId, itemId, drops.Count, sort);

        // Stackable branch (tSort 523, iSort 2/99) with an out-of-range/negative explicit quantity: legacy's
        // `tResult=2; break;` exits the outer switch directly, so the failure code survives onto the wire --
        // see StackableQuantityRejectedResult's own remarks. Every other case (including the non-stackable
        // per-unit branch creating nothing) keeps reporting AcceptedResult, matching legacy's own masking there.
        var resultCode = isStackable && drops.Count == 0 ? StackableQuantityRejectedResult : AcceptedResult;
        zoneSession.Send(new GenericActionResponse { Result = resultCode, Sort = sort, Data = data, RuneValue = 0 });
    }

    /// <summary>
    ///     Server/ts25zone/S07_MyGame03.cpp:505-527's stackable-vs-unique branch, restated: a stackable item
    ///     (<see cref="ContainerMatrix.IsStackableSort" />) always yields exactly one object; sort 505 (no
    ///     quantity supplied) or an explicit 0 forces the stack to the 999 cap, 1-999 is honored as given, and
    ///     anything else (nonzero, out of [1,999]) yields no object at all. A non-stackable item yields one
    ///     object per requested unit -- sort 505 or an explicit 0 resolves to exactly 1; sort 523 with a
    ///     positive count yields that many separate unit-quantity-1 objects, uncapped.
    /// </summary>
    private static List<TribeGroundItemDrop> ResolveDrops(int sort, int itemId, int requestedQuantity,
        byte itemCatalogSort)
    {
        var drops = new List<TribeGroundItemDrop>();

        if (ContainerMatrix.IsStackableSort(itemCatalogSort))
        {
            int quantity;
            if (sort != 523 || requestedQuantity == 0)
                quantity = GroundItemPickupPolicy.MaxStackQuantity;
            else if (requestedQuantity is >= 1 and <= GroundItemPickupPolicy.MaxStackQuantity)
                quantity = requestedQuantity;
            else
                quantity = 0; // Out-of-range nonzero quantity -- creation fails and StackableQuantityRejectedResult is reported.

            if (quantity > 0)
                drops.Add(new TribeGroundItemDrop(itemId, quantity, GroundItemEntity.GmCreateItemDropSort));
        }
        else
        {
            var resolvedCount = sort != 523 || requestedQuantity == 0 ? 1 : requestedQuantity;
            for (var i = 0; i < resolvedCount; i++)
                drops.Add(new TribeGroundItemDrop(itemId, 1, GroundItemEntity.GmCreateItemDropSort));
        }

        return drops;
    }
}
