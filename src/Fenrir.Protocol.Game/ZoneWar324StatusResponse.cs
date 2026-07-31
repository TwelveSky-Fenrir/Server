using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_324_TYPE_BATTLE_STATE Server/Header/Protocol/ZONE.h:1282-1286, ordre tSort puis tResult, inverse du 159 ; mort en M33/LNW33 : orphelin total, les seules occurrences dans Server/ sont les trois lignes de ZONE.h (struct, ZCP:1695, ZCS:1696).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar324Status,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar324StatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int Result { get; init; }
}
