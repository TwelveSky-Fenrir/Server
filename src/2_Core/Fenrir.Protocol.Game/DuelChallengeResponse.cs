using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelChallenge, ExpectedSize = 18)]
public readonly partial record struct DuelChallengeResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
