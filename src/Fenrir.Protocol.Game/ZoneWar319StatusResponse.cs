using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar319Status,
    ExpectedSize = 437)]
public readonly partial record struct ZoneWar319StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] Result { get; init; }

    [FixedArray(20)] public required int[] RankTribe { get; init; }

    [FixedArray(20)] public required int[] RankScore { get; init; }

    [FixedArray(20)] [FixedString(13)] public required string[] RankName { get; init; }
}
