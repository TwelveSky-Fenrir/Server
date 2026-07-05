using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_DEMAND_PSHOP_SEND (opcode 33), extracted from <see cref="ViewShopStallHandler" />.</summary>
public interface IViewShopStallService
{
    public ViewShopStallResponse View(ViewShopStallRequest packet, Zone zone, PlayerRuntimeState requester);
}
