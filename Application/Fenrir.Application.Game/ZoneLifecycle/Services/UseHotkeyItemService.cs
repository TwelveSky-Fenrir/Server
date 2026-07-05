using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>Business logic for CZ_USE_HOTKEY_ITEM_SEND (op22) -- see <c>UseHotkeyItemHandler</c>'s remarks.</summary>
public interface IUseHotkeyItemService
{
    bool IsOccupied(PlayerRuntimeState state, int page, int index);
}

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
