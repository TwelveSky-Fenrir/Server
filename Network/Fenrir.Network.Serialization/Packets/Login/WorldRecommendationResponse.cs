using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// AddKillOtherTribeN fields are never assigned by the legacy serializer -- always zero on the wire.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.WorldRecommendation,
    ExpectedSize = 13)]
public readonly partial record struct WorldRecommendationResponse : IOutgoingPacket
{
    public required int AddKillOtherTribe0 { get; init; }
    public required int AddKillOtherTribe1 { get; init; }
    public required int AddKillOtherTribe2 { get; init; }
}
