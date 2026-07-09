using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Sort: 1=candidacy (Value=slot 0-9), 3=vote for slot; Sort 2 (withdrawal) is compiled out.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeVote, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct TribeVoteRequest : IIncomingPacket<TribeVoteRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
