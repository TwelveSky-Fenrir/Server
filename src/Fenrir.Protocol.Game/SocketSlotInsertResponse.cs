using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_SOCKET_SLOT_INSERT_RECV Server/Header/Protocol/ZONE.h:1245-1249 ; mort en M33/LNW33 : emetteur Server/ts25zone/S05_MyTransfer.cpp:1576-1582 et handler Server/ts25zone/S04_MyWork02.cpp:14109-14194 compiles, mais le REGWORK1 de l'entrant jumeau est sous USE_SOCKET_GEM Server/ts25zone/S04_MyWork01.cpp:131-133, undef Server/Header/Protocol/DEFINE.h:105.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SocketSlotInsert,
    ExpectedSize = 17)]
public readonly partial record struct SocketSlotInsertResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(3)] public required int[] Value { get; init; }
}
