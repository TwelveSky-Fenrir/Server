using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Only Sort 1 (candidacy) and 3 (vote) are ever emitted; other paths are compiled out.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeVote, ExpectedSize = 13)]
public readonly partial record struct TribeVoteResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
