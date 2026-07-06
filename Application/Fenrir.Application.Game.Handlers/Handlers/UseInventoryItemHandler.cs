using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

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
///     Not modeled: the per-tick anti-flood throttle (mTickForUseInventoryItem) and the page-1
///     storage-extension gate (aInventoryDate, no <see cref="PlayerRuntimeState" /> field exists yet) -- both
///     orthogonal to which item family is used, and neither has an acquisition/observation path through any
///     opcode implemented so far.
/// </remarks>
public sealed class UseInventoryItemHandler(IUseInventoryItemService service)
    : IAsyncPacketHandler<UseInventoryItemRequest>
{
    public async ValueTask HandleAsync(UseInventoryItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page;
        var index = packet.Index;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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
