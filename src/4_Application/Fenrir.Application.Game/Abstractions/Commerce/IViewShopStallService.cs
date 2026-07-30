using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IViewShopStallService
{
    public ViewShopStallResponse View(ViewShopStallRequest packet, Zone zone, PlayerRuntimeState requester);
}
