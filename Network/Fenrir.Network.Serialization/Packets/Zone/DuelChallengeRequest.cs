using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DuelChallenge, ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct DuelChallengeRequest : IIncomingPacket<DuelChallengeRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
