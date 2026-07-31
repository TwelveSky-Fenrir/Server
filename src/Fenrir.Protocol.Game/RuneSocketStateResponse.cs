using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_RUNE_PROCESSDATA_RECV Server/Header/Protocol/ZONE.h:504-511, byte-identique a ZC_RUNE_SYSTEM_RECV (opcode 199) ; mort en M33/LNW33 : emetteur B_RUNE_PROCESSDATA_RECV (S05_MyTransfer.cpp:598-606) sans aucun appelant.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RuneSocketState,
    ExpectedSize = 21)]
public readonly partial record struct RuneSocketStateResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int ItemIndex { get; init; }

    public required int RuneIndex { get; init; }
}
