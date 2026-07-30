using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetCashCatalogService
{
    public GetCashCatalogResponse GetCatalog(PlayerRuntimeState? state);
}
