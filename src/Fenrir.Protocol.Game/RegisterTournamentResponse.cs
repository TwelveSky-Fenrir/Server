using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_REGISTER_TOURNAMENT_RECV Server/Header/Protocol/ZONE.h:1380-1383 ; mort en M33/LNW33 : unique appelant S04_MyWork02.cpp:15096 sous #ifdef TOURNAMENT_REGISTER, macro commentee DEFINE.h:41.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RegisterTournament,
    ExpectedSize = 5)]
public readonly partial record struct RegisterTournamentResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
