using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_REWARD_ITEM_SEND (opcode 154, contracts/04_commerce.md) -- the 7-day login-reward catalog +
///     this character's own claim cursor. world.RewardBundles has exactly 1 row in this build (verified,
///     RewardBundleItemRowDto's own header) -- hardcoded here rather than resolved dynamically, matching
///     that same single-bundle reality. Unlike the legacy's aggressive <c>Quit()</c> on ANY IPC/lookup
///     failure, an unknown character here just returns the neutral all-day-7 answer (Fenrir has no IPC
///     hop to fail in the first place -- a genuinely different failure surface, not a corrected behavior).
/// </summary>
public sealed class GetDailyRewardCatalogHandler(CharacterRepository characters, WorldDataCache worldData)
    : IAsyncPacketHandler<GetDailyRewardCatalogRequest>
{
    private const int RewardBundleId = 1;

    public async ValueTask HandleAsync(GetDailyRewardCatalogRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        var state = await characters.GetRewardClaimStateAsync(characterId, LegacyDate.Today(), cancellationToken);

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
