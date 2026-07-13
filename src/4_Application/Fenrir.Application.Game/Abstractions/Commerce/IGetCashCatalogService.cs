using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetCashCatalogService
{
    public GetCashCatalogResponse GetCatalog(PlayerRuntimeState? state);
}
