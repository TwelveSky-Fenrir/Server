using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRankingCurrent,
    ExpectedSize = 685)]
public readonly partial record struct HeroRankingCurrentResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required HeroRank HeroInfo { get; init; }
}
