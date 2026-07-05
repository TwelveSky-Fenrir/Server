using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_REWARD_ITEM_SEND (opcode 154) -- the 7-day login-reward catalog plus this character's claim
///     cursor. world.RewardBundles has exactly 1 row in this build, hardcoded here rather than resolved
///     dynamically.
/// </summary>
public sealed class GetDailyRewardCatalogHandler(ICharacterRepository characters, WorldDataCache worldData)
    : IAsyncPacketHandler<GetDailyRewardCatalogRequest>
{
    private const int RewardBundleId = 1;

    public async ValueTask HandleAsync(GetDailyRewardCatalogRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        var state = await characters.GetRewardClaimStateAsync(characterId, GameDate.Today(), cancellationToken);

        var rewardItems = new int[7];
        if (worldData.RewardBundleItemsByBundleId.TryGetValue(RewardBundleId, out var slots))
            foreach (var slot in slots)
                if (slot.SlotIndex is >= 1 and <= 7)
                    rewardItems[slot.SlotIndex - 1] = slot.ItemId ?? 0;

        session.Send(new GetDailyRewardCatalogResponse
        {
            RewardItem = rewardItems, RewardDay = state?.RewardClaimDay ?? 7
        });
    }
}
