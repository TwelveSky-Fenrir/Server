using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Always all zero on the wire, like WorldRecommendationResponse; last packet of the login train.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.WorldRecommendationFinal,
    ExpectedSize = 13)]
public readonly partial record struct WorldRecommendationFinalResponse : IOutgoingPacket
{
    public required int AddKillOtherTribe0 { get; init; }
    public required int AddKillOtherTribe1 { get; init; }
    public required int AddKillOtherTribe2 { get; init; }
}
