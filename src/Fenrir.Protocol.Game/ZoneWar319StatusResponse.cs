using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_319_TYPE_BATTLE_INFO Server/Header/Protocol/ZONE.h:1256-1260, TOP_RANK_319_INFO Server/Header/Protocol/STRUCT.h:1898-1902 aplati ici faute de type partage equivalent ; mort en M33/LNW33 : l'emetteur B_319_TYPE_BATTLE_INFO2 Server/ts25zone/S05_MyTransfer.cpp:1590-1602 n'a aucun appelant et ne fait meme pas de USEND, et ZONE319 n'est defini nulle part.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar319Status,
    ExpectedSize = 437)]
public readonly partial record struct ZoneWar319StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] Result { get; init; }

    [FixedArray(20)] public required int[] RankTribe { get; init; }

    [FixedArray(20)] public required int[] RankScore { get; init; }

    [FixedArray(20)] [FixedString(13)] public required string[] RankName { get; init; }
}
