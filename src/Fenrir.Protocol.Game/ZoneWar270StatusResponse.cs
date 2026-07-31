using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_270_TYPE_BATTLE_INFO Server/Header/Protocol/ZONE.h:998-1002 ; mort en M33/LNW33 : emetteur entierement commente Server/ts25zone/S05_MyTransfer.cpp:1313-1318.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar270Status,
    ExpectedSize = 9)]
public readonly partial record struct ZoneWar270StatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int RemainTime { get; init; }
}
