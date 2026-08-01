using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DecideChallengeFourGuild,
    ExpectedSize = 365)]
public readonly partial record struct FourGuildChallengeBoardResponse : IOutgoingPacket
{
    [FixedArray(28)] [FixedString(13)] public required string[] GuildChallenge { get; init; }
}
