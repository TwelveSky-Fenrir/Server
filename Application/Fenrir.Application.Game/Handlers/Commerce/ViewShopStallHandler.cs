using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_DEMAND_PSHOP_SEND (opcode 33, contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:6385-6423</c>)
///     -- inspect another LIVE personal shop stall. <c>SearchAvatar</c>-scoped (same-zone only, verified) --
///     resolved via THIS zone's own <see cref="Zone.Players" /> (never process-wide), matching every other
///     PShop action's zone-37-only reach.
/// </summary>
public sealed class ViewShopStallHandler : IInlinePacketHandler<ViewShopStallRequest>
{
    /// <summary>
    ///     Placeholder for "requester never opened a stall" (client-ignored). Must be a real FixedString/FixedArray
    ///     shape, never <c>default(PshopInfo)</c> -- its null <see cref="PshopInfo.Name" />/arrays can't serialize on the
    ///     wire.
    /// </summary>
    internal static readonly PshopInfo EmptyPshopInfo = new()
        { UniqueNumber = 0, Name = string.Empty, ItemInfo = new int[225], SocketInfo = new int[75] };

    public void Handle(in ViewShopStallRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var requester) ||
            requester is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        // Legacy trap (verified): on either error path the PshopInfo carries the REQUESTER's own stall,
        // not the target's -- content the client is documented to ignore.
        var ownListing = requester.PshopListing ?? EmptyPshopInfo;

        if (target is null)
        {
            session.Send(new ViewShopStallResponse { Result = 1, PshopInfo = ownListing });
            return;
        }

        if (!target.PshopOpen || target.PshopListing is not { } listing)
        {
            session.Send(new ViewShopStallResponse { Result = 2, PshopInfo = ownListing });
            return;
        }

        session.Send(new ViewShopStallResponse { Result = 0, PshopInfo = listing });
    }
}
