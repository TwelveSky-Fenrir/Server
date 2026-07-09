using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmCreateItemService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1036-1095 (case 505/523 body: privilege gate, id-range/catalog
///     checks, quantity resolution, stackable-vs-unique branches, the final unconditional result-code
///     overwrite) ; Server/ts25zone/S04_MyWork04.cpp:7 (<c>eLocation</c> macro resolving to the acting user's
///     own live location -- mirrored here by always spawning at <c>state.Pos*</c>, the GM's own
///     <see cref="PlayerRuntimeState" />, never any other target) ; Server/ts25zone/S07_MyGame03.cpp:505-527
///     (stackable-item quantity default-to-cap-on-zero and 1-999 bound enforcement) ;
///     Server/ts25zone/S07_MyGame03.cpp:578-594 (world item-object pool slot search/exhaustion -- see this
///     type's own remarks for why that failure path is unreachable in Fenrir's own reimplementation) ;
///     Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:289-305 and Server/ts25zone/H06_MyUpperCom.h:268
///     (audit-log call for item creation -- see this type's own remarks for the inexact field mapping) ;
///     Server/Header/Protocol/DEFINE.h:611 (MAX_ITEM_DUPLICATION_NUM = 999, ported as
///     <see cref="GroundItemPickupPolicy.MaxStackQuantity" />) ; Server/Header/Protocol/DEFINE.h:520
///     (GM-drop-source code, ported as <see cref="GroundItemEntity.GmCreateItemDropSort" /> -- see that
///     constant's own remarks for its unconfirmed-value caveat).
/// </summary>
/// <remarks>
///     Legacy's world item-object pool is a fixed-size array whose exhaustion is a real, observable failure
///     mode this command's own privilege-preserving "always report accepted" defect masks from the client.
///     Fenrir's own <see cref="Zone.SpawnGroundItem" /> (reached here via <see cref="TribeProgressZoneCommand.DropItems" />)
///     is backed by an unbounded <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />
///     with no capacity ceiling, so that specific failure mode cannot occur in this implementation -- every
///     creation this method decides to attempt (after the id-range/catalog and, for a stackable item, the
///     1-999 quantity-bound checks) unconditionally succeeds. This is a genuine, documented architectural
///     divergence from legacy, not a bug: the client-visible behavior (result code always accepted once the
///     id/catalog gate passes) is unaffected either way, since legacy already masks that exact failure mode
///     from the wire.
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
    /// <summary>Legacy tResult's own default-initialized/rejected value (same convention as GmBlockAvatarService's ResultTargetNotFound).</summary>
    private const int RejectedResult = 1;

    /// <summary>
    ///     Forced unconditionally once the id-range/catalog gate passes, regardless of whether creation
    ///     actually happened -- see this type's own remarks and the source contract's "Error/failure
    ///     semantics" section for why this masking is preserved rather than "fixed."
    /// </summary>
    private const int AcceptedResult = 0;

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
            zoneSession.Send(new GenericActionResponse { Result = RejectedResult, Sort = sort, Data = data, RuneValue = 0 });
            return;
        }

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

        // Unconditionally accepted from here on -- see this type's own remarks for the legacy-defect rationale,
        // even when `drops` ended up empty (a stackable-item quantity outside [1,999], nonzero).
        zoneSession.Send(new GenericActionResponse { Result = AcceptedResult, Sort = sort, Data = data, RuneValue = 0 });
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
                quantity = 0; // Out-of-range nonzero quantity -- creation fails silently (result stays accepted).

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
