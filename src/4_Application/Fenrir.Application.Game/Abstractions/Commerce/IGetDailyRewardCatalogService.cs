using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetDailyRewardCatalogService
{
    public ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int characterId,
        CancellationToken cancellationToken);
}
