using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_319_TYPE_BATTLE_STATE Server/Header/Protocol/ZONE.h:1262-1266 ; mort en M33/LNW33 : aucun emetteur n'a jamais existe, les 3 seules references du symbole sont Server/Header/Protocol/ZONE.h:1266, :1687 et :1688, et ZONE319 n'est defini nulle part.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar319Phase,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar319PhaseResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ZoneNumber { get; init; }
}
