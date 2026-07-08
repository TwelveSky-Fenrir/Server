using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelChallenge, ExpectedSize = 18)]
public readonly record struct DuelChallengeResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
