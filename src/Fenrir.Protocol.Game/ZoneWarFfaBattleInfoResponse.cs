using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWarFfaBattleInfo,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWarFfaBattleInfoResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
