using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op23, CZ_USE_INVENTORY_ITEM_SEND -- three families out of the ~6300-line legacy switch are modeled:
///     the Bottle family (iSort==26, S04_MyWork03.cpp:2448, via <c>BottleResolver.ResolveAcquire</c>)
///     and two members of the iSort==3 grab-bag of "right-click, single-purpose" items -- world.Items itself
///     shows iSort==3 covers wildly different behaviors (Guild Emblem register, Guild Boss Box loot roll,
///     Guild Scroll recharge, Faction Transfer Scroll) per specific ItemId, not per sort, so
///     <see cref="IUseInventoryItemService" /> dispatches on the item id itself. Every other iSort/iIndex
///     family (mounts, skills, costumes, cash-shop timers, the rest of the iSort==3 bucket...) still replies
///     with a clean Result=1 failure -- out of scope for this pass (see the recon report's own Batch A
///     conclusion).
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
            var response = await service.ResolveAsync(zone, state, characterId, (byte)page, (byte)index,
                cancellationToken);
            session.Send(response);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
