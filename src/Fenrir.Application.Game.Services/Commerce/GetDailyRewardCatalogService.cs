using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class GetDailyRewardCatalogService(ICharacterRepository characters, WorldDataCache worldData)
    : IGetDailyRewardCatalogService
{
    private const int RewardBundleId = 1;

    public async ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int accountId,
        CancellationToken cancellationToken)
    {
        var state = await characters.GetAccountRewardClaimStateAsync(accountId, GameDate.Today(), cancellationToken);

        var rewardItems = new int[7];
        if (worldData.RewardBundleItemsByBundleId.TryGetValue(RewardBundleId, out var slots))
            foreach (var slot in slots)
                if (slot.SlotIndex is >= 1 and <= 7)
                    rewardItems[slot.SlotIndex - 1] = slot.ItemId ?? 0;

        return new GetDailyRewardCatalogResponse
        {
            RewardItem = rewardItems, RewardDay = state?.RewardClaimDay ?? 7
        };
    }
}
