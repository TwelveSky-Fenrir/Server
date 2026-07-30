using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DuelChallenge, ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct DuelChallengeRequest : IIncomingPacket<DuelChallengeRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
