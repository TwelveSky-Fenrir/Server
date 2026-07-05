using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class UseHotkeyItemService : IUseHotkeyItemService
{
    public bool IsOccupied(PlayerRuntimeState state, int page, int index)
    {
        // Compare as int before narrowing to byte: an untrusted page value could otherwise wrap and alias a real container id.
        var isInventoryPage = page == ContainerMatrix.InventoryPage0 || page == ContainerMatrix.InventoryPage1;
        return isInventoryPage && ContainerMatrix.IsValidSlot((byte)page, index) &&
               state.Inventory.GetSlot((byte)page, (byte)index) is not null;
    }
}
