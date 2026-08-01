using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DecideChallengeFourGuild,
    ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FourGuildChallengeDecisionRequest
    : IIncomingPacket<FourGuildChallengeDecisionRequest>
{
    public required int Tribe { get; init; }
    [FixedString(13)] public required string GuildName { get; init; }
}
