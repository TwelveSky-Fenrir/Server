using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DuelChallenge, ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct DuelChallengeRequest : IIncomingPacket<DuelChallengeRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
