using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout ZC_DICE_BATTLE_RECV Server/Header/Protocol/ZONE.h:1070-1075 ; mort en M33/LNW33 : aucun emetteur, le seul candidat est commente Server/ts25zone/S05_MyTransfer.cpp:1402.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DiceBattleResult,
    ExpectedSize = 13)]
public readonly partial record struct DiceBattleResultResponse : IOutgoingPacket
{
    public required int Value00 { get; init; }

    public required int Value01 { get; init; }

    public required int Value02 { get; init; }
}
