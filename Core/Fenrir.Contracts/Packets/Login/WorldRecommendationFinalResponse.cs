using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_RECOMMAND_WORLD2_RECV (LOGIN.h l.216-222, same struct as op 24): the very last packet of every login
///     train (login protocol report §5.26). Three ints, always zero, no XOR — see
///     <see cref="WorldRecommendationResponse" />.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.WorldRecommendationFinal,
    ExpectedSize = 13)]
public readonly partial record struct WorldRecommendationFinalResponse : IOutgoingPacket
{
    public required int AddKillOtherTribe0 { get; init; }
    public required int AddKillOtherTribe1 { get; init; }
    public required int AddKillOtherTribe2 { get; init; }
}
