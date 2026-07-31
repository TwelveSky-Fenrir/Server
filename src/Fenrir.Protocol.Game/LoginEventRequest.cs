using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Aucun struct legacy : CLIENT_PACKET nu, S_LOGIN_EVENT_SEND1 vaut le litteral 9 CLIENT.h:690-691 ; mort en M33/LNW33 : REGWORK1 commente S04_MyWork01.cpp:109.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.LoginEvent1, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct LoginEventRequest : IIncomingPacket<LoginEventRequest>
{
}
