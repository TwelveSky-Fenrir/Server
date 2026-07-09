using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Result: 0=ok, 1=already claimed today, 2=inventory full (-1 positions for 1/2). Trailing trio is Page/InvenX/InvenY, not Page/Index.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ClaimDailyReward,
    ExpectedSize = 41)]
public readonly record struct ClaimDailyRewardResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    public required int InvenPage { get; init; }
    public required int InvenX { get; init; }
    public required int InvenY { get; init; }
}
