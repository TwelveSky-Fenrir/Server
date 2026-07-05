using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_DEMAND_PSHOP_SEND (opcode 33), extracted from <see cref="ViewShopStallHandler" />.</summary>
public interface IViewShopStallService
{
    ViewShopStallResponse View(ViewShopStallRequest packet, Zone zone, PlayerRuntimeState requester);
}

public sealed class ViewShopStallService : IViewShopStallService
{
    // Placeholder for "requester never opened a stall" -- must not be default(PshopInfo): its null
    // Name/arrays can't serialize on the wire.
    private static readonly PshopInfo EmptyPshopInfo = new()
        { UniqueNumber = 0, Name = string.Empty, ItemInfo = new int[225], SocketInfo = new int[75] };

    public ViewShopStallResponse View(ViewShopStallRequest packet, Zone zone, PlayerRuntimeState requester)
    {
        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        // Legacy trap: on either error path the PshopInfo carries the REQUESTER's own stall, not the target's.
        var ownListing = requester.PshopListing ?? EmptyPshopInfo;

        if (target is null)
            return new ViewShopStallResponse { Result = 1, PshopInfo = ownListing };

        if (!target.PshopOpen || target.PshopListing is not { } listing)
            return new ViewShopStallResponse { Result = 2, PshopInfo = ownListing };

        return new ViewShopStallResponse { Result = 0, PshopInfo = listing };
    }
}
