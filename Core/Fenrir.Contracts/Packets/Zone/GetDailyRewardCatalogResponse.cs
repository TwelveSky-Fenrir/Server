using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GET_REWARD_ITEM_RECV (ZONE.h:1390-1394) — reply to CZ_GET_REWARD_ITEM_SEND, sourced from
///     <c>mEXTRA.mRecv_RewardItem</c> + the shm playuser's <c>uRewardClaimDay</c>. <c>RewardItem</c> = 7
///     item IDs (one per day); <c>RewardDay</c> = the next claimable day (0..7; 7 = all claimed). Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetDailyRewardCatalog,
    ExpectedSize = 33)]
public readonly partial record struct GetDailyRewardCatalogResponse : IOutgoingPacket
{
    [FixedArray(7)] public required int[] RewardItem { get; init; }
    public required int RewardDay { get; init; }
}
