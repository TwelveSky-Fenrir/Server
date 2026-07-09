using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRankingPrevious,
    ExpectedSize = 685)]
public readonly record struct HeroRankingPreviousResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required HeroRank HeroInfo { get; init; }
}
