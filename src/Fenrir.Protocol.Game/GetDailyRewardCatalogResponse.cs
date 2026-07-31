using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetDailyRewardCatalog,
    ExpectedSize = 33)]
public readonly partial record struct GetDailyRewardCatalogResponse : IOutgoingPacket
{
    [FixedArray(7)] public required int[] RewardItem { get; init; }
    public required int RewardDay { get; init; }
}
