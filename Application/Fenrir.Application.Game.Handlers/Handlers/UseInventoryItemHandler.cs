using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op23, CZ_USE_INVENTORY_ITEM_SEND -- see <see cref="IUseInventoryItemService" />'s own remarks for the
///     full, currently-growing list of modeled item-id families. world.Items itself shows iSort==3 alone
///     covers wildly different behaviors (Guild Emblem register, Guild Boss Box loot roll, Guild Scroll
///     recharge, Faction Transfer Scroll) per specific ItemId, not per sort, so
///     <see cref="IUseInventoryItemService" /> dispatches on the item id itself throughout. Every unmodeled
///     iSort/iIndex family still replies with a clean Result=1 failure rather than a disconnect.
/// </summary>
/// <remarks>
///     Not modeled: the page-1 storage-extension gate (aInventoryDate, no <see cref="PlayerRuntimeState" />
///     field exists yet) -- orthogonal to which item family is used, and it has no acquisition/observation
///     path through any opcode implemented so far.
/// </remarks>
/// <remarks>
///     Per-avatar, item-agnostic anti-flood gate (legacy <c>mTickForUseInventoryItem</c>,
///     Server/ts25zone/S04_MyWork03.cpp:1861-1866) -- the very first check this handler performs, before any
///     item-specific dispatch, matching legacy's own ordering. Legacy rejects unless its discrete ~499 ms
///     logic-tick counter (TimeLogic=500, decremented once per Server/Header/socket.h:8) has advanced by at
///     least one full tick since the last accepted use, with zero burst tolerance. This models that same
///     "at least one full legacy tick must elapse" rule as a continuous wall-clock comparison against
///     <see cref="PlayerRuntimeState.LastItemUseUtc" /> (<see cref="SimulationClock.LegacyTick" />, 500 ms)
///     rather than a discrete Zone-wide tick counter, since this handler runs on the request thread, not the
///     Zone tick thread that owns the only Zone-wide simulated clock in this codebase -- the same
///     request-thread/tick-thread split <see cref="PlayerRuntimeState.LastMoveUtc" />/
///     <c>MovementRules.IsPlausible</c> already models for an analogous reason. On rejection, the client
///     receives the same generic Result=1 failure <see cref="IUseInventoryItemService" /> uses for every
///     other rejection reason (legacy's own <c>wUseInvFail()</c>, Server/ts25zone/S04_MyWork03.cpp:103) --
///     it cannot distinguish "rate-limited" from any other item-use failure. On success, the field is
///     stamped unconditionally before the service call runs, mirroring legacy's own bookkeeping (never
///     rolled back even if a later stage of the same request fails).
/// </remarks>
public sealed class UseInventoryItemHandler(IUseInventoryItemService service, ILogger<UseInventoryItemHandler> logger)
    : IAsyncPacketHandler<UseInventoryItemRequest>
{
    public async ValueTask HandleAsync(UseInventoryItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest received ({Page}:{Index}, value {Value})",
                zoneSession.SessionId, characterId, packet.Page, packet.Index, packet.Value);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        var page = packet.Page;
        var index = packet.Index;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index))
        {
            logger.LogInformation(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest aborted, invalid slot ({Page}:{Index})",
                zoneSession.SessionId, characterId, page, index);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Per-avatar, item-agnostic anti-flood gate -- see this class's own remarks. Runs ahead of the
        // EconomyActionLock/service call since legacy performs this same check before any item-specific
        // dispatch, and it depends on nothing the lock protects.
        var now = DateTime.UtcNow;
        if (now - state.LastItemUseUtc < SimulationClock.LegacyTick)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UseInventoryItemRequest rejected, anti-flood gate ({Page}:{Index})",
                zoneSession.SessionId, characterId, page, index);
            session.Send(new UseInventoryItemResponse
                { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 });
            return;
        }

        state.LastItemUseUtc = now;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var response = await service.ResolveAsync(zone, state, characterId, accountId, (byte)page, (byte)index,
                packet.Value, cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
