using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op22, CZ_USE_HOTKEY_ITEM_SEND (contracts/03_inventory_craft.md, W_USE_HOTKEY_ITEM_SEND,
///     S04_MyWork02.cpp:2203) -- NOT a hotkey ASSIGNMENT (that's PROCESS_DATA tSort 204/205/211/214/216,
///     unimplemented -- see <c>GenericActionHandler</c>'s own remarks and this task's openIssues). This is
///     the client clicking an already-bound hotkey slot: <see cref="UseHotkeyItemRequest.Page1" />/
///     <see cref="UseHotkeyItemRequest.Index1" /> name the INVENTORY page/slot the client's own local hotkey
///     table already resolved (it received the full table at world entry, AvatarInfo.HotKey) -- the wire
///     struct carries no separate "which hotkey" identifier at all, only the resolved item location.
/// </summary>
/// <remarks>
///     SCOPE (V2 foundations): validates the referenced slot is in-bounds and currently occupied and echoes a
///     clean result -- it does NOT yet apply any consumable "use" EFFECT (potion stat gain, box opening,
///     rune, etc: report 04_mega_switches.md §2's ~6300-line USE_INVENTORY_ITEM switch) or decrement
///     <c>Quantity</c>, since that whole item-use pipeline is explicitly out of this pass's scope (Phase C/V5
///     craft-and-use, V9 progression). Tracked as an open issue rather than silently guessed at. Inline/zero
///     SQL: nothing here mutates persisted state yet.
/// </remarks>
public sealed class UseHotkeyItemHandler : IInlinePacketHandler<UseHotkeyItemRequest>
{
    public void Handle(in UseHotkeyItemRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        // Benign staleness (mid-handoff/disconnect) -- same defensive posture as every other InWorld handler;
        // no reply is expected by anything if the character is not resolvable right now.
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page1;
        var index = packet.Index1;

        // Page must be a real inventory page BEFORE any byte cast: an untrusted int narrowed to byte can wrap
        // around and alias a real container id (e.g. 256 -> 0) -- comparing as plain ints first closes that.
        var isInventoryPage = page == ContainerMatrix.InventoryPage0 || page == ContainerMatrix.InventoryPage1;
        var occupied = isInventoryPage && ContainerMatrix.IsValidSlot((byte)page, index) &&
                       state.Inventory.GetSlot((byte)page, (byte)index) is not null;

        session.Send(new UseHotkeyItemResponse { Result = occupied ? 0 : 1, Page = page, Index = index });
    }
}
