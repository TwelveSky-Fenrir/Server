using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_CANCEL_HOISUNDO_OK_RECV Server/Header/Protocol/ZONE.h:1140-1145 ; mort en M33/LNW33 : orphelin total, les seules occurrences du nom dans tout Server/ sont les trois lignes de ZONE.h (struct, ZCP, ZCS).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CancelHoisundo,
    ExpectedSize = 22)]
public readonly partial record struct CancelHoisundoResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }

    public required int HoisundoIndex { get; init; }

    [FixedString(13)] public required string HoisundoName { get; init; }
}
