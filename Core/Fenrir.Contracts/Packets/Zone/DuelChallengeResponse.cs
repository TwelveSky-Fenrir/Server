using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelChallenge, ExpectedSize = 18)]
public readonly partial record struct DuelChallengeResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
