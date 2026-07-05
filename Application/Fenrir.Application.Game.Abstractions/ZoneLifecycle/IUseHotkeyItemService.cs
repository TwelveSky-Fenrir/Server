using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>Business logic for CZ_USE_HOTKEY_ITEM_SEND (op22) -- see <c>UseHotkeyItemHandler</c>'s remarks.</summary>
public interface IUseHotkeyItemService
{
    public bool IsOccupied(PlayerRuntimeState state, int page, int index);
}
