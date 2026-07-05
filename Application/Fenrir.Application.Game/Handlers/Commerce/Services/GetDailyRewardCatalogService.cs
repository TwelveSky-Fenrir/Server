using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_GET_REWARD_ITEM_SEND (opcode 154), extracted from <see cref="GetDailyRewardCatalogHandler" />.</summary>
public interface IGetDailyRewardCatalogService
{
    ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int characterId, CancellationToken cancellationToken);
}

/// <summary>
///     world.RewardBundles has exactly 1 row in this build, hardcoded here rather than resolved dynamically.
/// </summary>
public sealed class GetDailyRewardCatalogService(ICharacterRepository characters, WorldDataCache worldData)
    : IGetDailyRewardCatalogService
{
    private const int RewardBundleId = 1;

    public async ValueTask<GetDailyRewardCatalogResponse> GetCatalogAsync(int characterId,
        CancellationToken cancellationToken)
    {
        var state = await characters.GetRewardClaimStateAsync(characterId, GameDate.Today(), cancellationToken);

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
