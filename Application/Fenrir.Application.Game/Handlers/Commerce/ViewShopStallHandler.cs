using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>CZ_DEMAND_PSHOP_SEND (opcode 33) -- inspect another live personal shop stall, same-zone only.</summary>
public sealed class ViewShopStallHandler : IInlinePacketHandler<ViewShopStallRequest>
{
    // Placeholder for "requester never opened a stall" -- must not be default(PshopInfo): its null
    // Name/arrays can't serialize on the wire.
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

        // Legacy trap: on either error path the PshopInfo carries the REQUESTER's own stall, not the target's.
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
