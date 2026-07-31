using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeVote, ExpectedSize = 13)]
public readonly partial record struct TribeVoteResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
