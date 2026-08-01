using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetDailyRewardCatalogService
{
    public ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int accountId,
        CancellationToken cancellationToken);
}
