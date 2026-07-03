using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_CLAIM_REWARD_ITEM_RECV (ZONE.h:1396-1403) — reply to CZ_CLAIM_REWARD_ITEM_SEND. <c>Result</c>
///     0 = ok (<c>Value[0]</c>=itemID, <c>Value[3]</c>=1 if iSort==99 else 0; real inventory position),
///     1 = already claimed today, 2 = inventory full (positions are -1 for cases 1/2). ATTENTION: the
///     trailing trio is Page/<see cref="InvenX" />/<see cref="InvenY" /> (not Page/Index). Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ClaimDailyReward,
    ExpectedSize = 41)]
public readonly partial record struct ClaimDailyRewardResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    public required int InvenPage { get; init; }
    public required int InvenX { get; init; }
    public required int InvenY { get; init; }
}
