using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class ViewShopStallService(ILogger<ViewShopStallService> logger) : IViewShopStallService
{
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

        var ownListing = requester.PshopListing ?? EmptyPshopInfo;

        if (target is null)
        {
            logger.LogDebug(
                "View shop stall: character {RequesterId} target {TargetAvatarName} not found in zone {MapId}",
                requester.CharacterId, packet.AvatarName, zone.MapId);
            return new ViewShopStallResponse { Result = 1, PshopInfo = ownListing };
        }

        if (!target.PshopOpen || target.PshopListing is not { } listing)
        {
            logger.LogDebug("View shop stall: character {RequesterId} target {TargetCharacterId} has no shop open",
                requester.CharacterId, target.CharacterId);
            return new ViewShopStallResponse { Result = 2, PshopInfo = ownListing };
        }

        logger.LogDebug("View shop stall: character {RequesterId} viewed target {TargetCharacterId}'s stall",
            requester.CharacterId, target.CharacterId);
        return new ViewShopStallResponse { Result = 0, PshopInfo = listing };
    }
}
