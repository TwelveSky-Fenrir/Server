using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRankingPrevious,
    ExpectedSize = 685)]
public readonly partial record struct HeroRankingPreviousResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required HeroRank HeroInfo { get; init; }
}
