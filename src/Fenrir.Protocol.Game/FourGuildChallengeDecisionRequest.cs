using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_DECIDE_CHALLENGE_FOURGUILD_SEND Server/Header/Protocol/CLIENT.h:436-440 ; mort en M33 : opcode non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DecideChallengeFourGuild,
    ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FourGuildChallengeDecisionRequest
    : IIncomingPacket<FourGuildChallengeDecisionRequest>
{
    public required int Tribe { get; init; }
    [FixedString(13)] public required string GuildName { get; init; }
}
