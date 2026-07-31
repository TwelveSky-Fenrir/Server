using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// ZC_FFA_TYPE_BATTLE_INFO, opcode 200 (Server/Header/Protocol/ZONE.h:1591). A ne pas confondre avec
// ZC_335_TYPE_BATTLE_COUNTDOWN, opcode 198 (:1724) : meme forme a 5 octets, donc aucun garde ne les distingue.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWarFfaBattleInfo,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWarFfaBattleInfoResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
