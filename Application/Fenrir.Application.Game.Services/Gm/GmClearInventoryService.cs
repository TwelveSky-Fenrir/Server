using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmClearInventoryService" />'s own remarks for the wire-level contract summary and the
///     documented Fenrir-data-model divergence around expiration-date preservation. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:2084-2111 (full case 701 body: the uUserSort &gt;= 1 gate, the
///     unconditional success-indicator assignment BEFORE the wipe runs, the page-selector resolution and its
///     out-of-range-defaults-to-both-pages behavior, and the per-slot clearing of item-identity/quantity and
///     socket/gem data while leaving the expiration-date field untouched) ; Server/Header/Protocol/DEFINE.h:
///     287-289 (inventory page count / per-page slot count this command iterates over) ;
///     Server/Header/Protocol/STRUCT.h:358 (item-data field), :458 (socket/gem-data field), :561 (the distinct
///     expiration-date field this command's own wipe does NOT touch) ; Server/ts25zone/S04_MyWork02.cpp:
///     7940-7961 (the separate, pre-existing plain-text GM chat command performing a similar but not identical
///     wipe -- cited only as contrast, not shared implementation, per IGmClearInventoryService's own remarks).
/// </summary>
public sealed class GmClearInventoryService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<GmClearInventoryService> logger) : IGmClearInventoryService
{
    private const int Sort = 701;

    /// <summary>Unconditional once the tier gate passes -- see this type's own remarks / the source contract's own Outputs.</summary>
    private const int AcceptedResult = 0;

    private static readonly ImmutableDictionary<byte, ItemStack> EmptyContainer =
        ImmutableDictionary<byte, ItemStack>.Empty;

    private static readonly IReadOnlyList<CharacterItemSlotTvp> EmptyTvps = [];

    public async ValueTask HandleAsync(GmClearInventoryPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM_CLEAR_INVENTORY command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // 0 -> page0 only ; 1 -> page1 only ; anything else (negative, or 2+) -> both pages. Never rejected --
        // see this type's own remarks.
        bool clearPage0;
        bool clearPage1;
        switch (packet.PageSelector)
        {
            case 0:
                clearPage0 = true;
                clearPage1 = false;
                break;
            case 1:
                clearPage0 = false;
                clearPage1 = true;
                break;
            default:
                clearPage0 = true;
                clearPage1 = true;
                break;
        }

        // Two independent single-container replacements rather than the atomic ICharacterRepository.
        // ReplaceTwoContainersAsync primitive MoveContainerAsync uses for a genuine cross-container item move:
        // there is no cross-container invariant at risk here (each write is an independent "set this page to
        // empty," never a duplication/loss-prone move of value from one container into another), so a crash
        // between the two writes is self-healing (one page cleared, the other untouched, safely retryable) --
        // not the kind of interrupted-transfer risk ReplaceTwoContainersAsync exists to close. This is a
        // deliberate, documented simplification for this low-frequency admin-only command, not an oversight.
        if (clearPage0)
            await characters.ReplaceContainerAsync(state.CharacterId, ContainerMatrix.InventoryPage0, EmptyTvps,
                cancellationToken);
        if (clearPage1)
            await characters.ReplaceContainerAsync(state.CharacterId, ContainerMatrix.InventoryPage1, EmptyTvps,
                cancellationToken);

        var containers = clearPage0 && clearPage1
            ? ImmutableArray.Create(
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, EmptyContainer),
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, EmptyContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(
                    clearPage0 ? ContainerMatrix.InventoryPage0 : ContainerMatrix.InventoryPage1, EmptyContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(state.CharacterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped GM_CLEAR_INVENTORY mirror for character {CharacterId}",
                zone.MapId, state.CharacterId);

        // Fenrir-authored audit trail addition -- see GmDuelAndInventoryActionEventCodes.ClearInventory's own
        // remarks for why (no confirmed legacy GL_* call for this specific command, and this project's own
        // standing "every mutation gets an audit record, unconditionally" rule).
        await eventLog.LogAsync(GmDuelAndInventoryActionEventCodes.ClearInventory, EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null, 1,
            $"PageSelector={packet.PageSelector};ClearedPage0={clearPage0};ClearedPage1={clearPage1}",
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Basic-tier GM_CLEAR_INVENTORY command (pageSelector {PageSelector}, clearedPage0={ClearedPage0}, clearedPage1={ClearedPage1})",
            state.CharacterId, packet.PageSelector, clearPage0, clearPage1);

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}
