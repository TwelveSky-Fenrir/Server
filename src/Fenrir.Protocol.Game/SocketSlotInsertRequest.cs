using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// CLIENT.h:485-491 CZ_SOCKET_SLOT_INSERT_SEND; mort: REGWORK1 sous USE_SOCKET_GEM S04_MyWork01.cpp:131-133, #undef DEFINE.h:105.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SocketSlotInsert, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct SocketSlotInsertRequest : IIncomingPacket<SocketSlotInsertRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
