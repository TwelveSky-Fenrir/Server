using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.WorldRecommendation,
    ExpectedSize = 13)]
public readonly partial record struct WorldRecommendationResponse : IOutgoingPacket
{
    public required int AddKillOtherTribe0 { get; init; }
    public required int AddKillOtherTribe1 { get; init; }
    public required int AddKillOtherTribe2 { get; init; }
}
