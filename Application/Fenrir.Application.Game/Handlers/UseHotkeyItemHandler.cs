using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op22, CZ_USE_HOTKEY_ITEM_SEND -- NOT a hotkey assignment; the client already resolved the hotkey to an
///     inventory page/slot locally, so this only validates that slot is occupied and echoes the result.
/// </summary>
/// <remarks>Does not yet apply the item's use effect or decrement quantity -- out of scope for this pass.</remarks>
public sealed class UseHotkeyItemHandler(IUseHotkeyItemService service) : IInlinePacketHandler<UseHotkeyItemRequest>
{
    public void Handle(in UseHotkeyItemRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page1;
        var index = packet.Index1;

        var occupied = service.IsOccupied(state, page, index);

        session.Send(new UseHotkeyItemResponse { Result = occupied ? 0 : 1, Page = page, Index = index });
    }
}
