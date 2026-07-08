using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_GET_REWARD_ITEM_SEND (opcode 154), extracted from
///     <see cref="GetDailyRewardCatalogHandler" />.
/// </summary>
public interface IGetDailyRewardCatalogService
{
    public ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int characterId,
        CancellationToken cancellationToken);
}
