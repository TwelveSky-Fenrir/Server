using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_319_TYPE_BATTLE_TIME Server/Header/Protocol/ZONE.h:1251-1254 ; mort en M33/LNW33 : l'emetteur B_319_TYPE_BATTLE_INFO1 Server/ts25zone/S05_MyTransfer.cpp:1584-1588 n'a aucun appelant et ne fait meme pas de USEND, et ZONE319 n'est defini nulle part.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar319Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar319CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
