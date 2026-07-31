using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// ZC_335_TYPE_BATTLE_COUNTDOWN, opcode 198 (Server/Header/Protocol/ZONE.h:1724). Aucun emetteur Fenrir :
// les deux sites existants emettent l'opcode 200 (ZoneWarFfaBattleInfoResponse), qui a la meme forme.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar335Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar335CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
