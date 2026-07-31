using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// CLIENT.h:155-161 CZ_RAGE_BUFF_SEND corps vide; mort: W_RAGE_BUFF_SEND commente S04_MyWork02.cpp:13830, aucun REGWORK.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RageBuff, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct RageBuffRequest : IIncomingPacket<RageBuffRequest>
{
}
