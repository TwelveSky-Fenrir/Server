using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_TRAP_POSITION_RECV ZONE.h:1288-1292; mort en M33/LNW33: emetteur compile S05_MyTransfer.cpp:1646-1651 mais zero appelant.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TrapPosition,
    ExpectedSize = 9)]
public readonly partial record struct TrapPositionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Value { get; init; }
}
