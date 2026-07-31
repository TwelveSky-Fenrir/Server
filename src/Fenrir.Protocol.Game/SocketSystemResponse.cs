using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_SOCKET_ITEM_RECV Server/Header/Protocol/ZONE.h:1089-1094 ; mort en M33/LNW33 : emetteur compile Server/ts25zone/S05_MyTransfer.cpp:1406-1418 mais son unique appelant Server/ts25zone/S04_MyWork02.cpp:13135 est sous USE_SOCKET_GEM, undef Server/Header/Protocol/DEFINE.h:105.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SocketSystem,
    ExpectedSize = 21)]
public readonly partial record struct SocketSystemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Sort { get; init; }

    [FixedArray(3)] public required int[] Value { get; init; }
}
