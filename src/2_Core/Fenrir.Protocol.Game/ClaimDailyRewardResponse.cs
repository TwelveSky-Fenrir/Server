using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

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
