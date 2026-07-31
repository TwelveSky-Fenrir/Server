using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_DECIDE_CHALLENGE_FOURGUILD_RECV ZONE.h:1192-1195, champ unique FOURGUILD_CHALLENGE.tGuildChallenge[4][7][13] STRUCT.h:1224-1227 aplati row-major en 28 noms; mort en M33/LNW33: unique appelant commente S04_MyWork02.cpp:953.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DecideChallengeFourGuild,
    ExpectedSize = 365)]
public readonly partial record struct FourGuildChallengeBoardResponse : IOutgoingPacket
{
    [FixedArray(28)] [FixedString(13)] public required string[] GuildChallenge { get; init; }
}
