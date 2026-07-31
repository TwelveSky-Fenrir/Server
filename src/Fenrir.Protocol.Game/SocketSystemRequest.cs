using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_SOCKET_SYSTEM_SEND CLIENT.h:406-411 ; mort en M33/LNW33 : REGWORK1 sous #ifdef USE_SOCKET_GEM S04_MyWork01.cpp:104-106 et #undef USE_SOCKET_GEM DEFINE.h:105.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SocketSystem, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct SocketSystemRequest : IIncomingPacket<SocketSystemRequest>
{
    public required int Sort { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
