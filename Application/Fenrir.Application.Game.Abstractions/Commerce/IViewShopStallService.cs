using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IViewShopStallService
{
    public ViewShopStallResponse View(ViewShopStallRequest packet, Zone zone, PlayerRuntimeState requester);
}
