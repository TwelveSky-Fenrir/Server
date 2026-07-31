using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// CLIENT.h:155-161 CZ_REGISTER_TOURNAMENT_SEND corps vide; mort: REGWORK1 sous TOURNAMENT_REGISTER S04_MyWork01.cpp:141-143, commente DEFINE.h:41.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RegisterTournament, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct RegisterTournamentRequest : IIncomingPacket<RegisterTournamentRequest>
{
}
