using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_BROADCAST_CHUGSOUNG_INFO (ZONE.h:1228-1232) — 2 × <c>int[12]</c> (2 rows of 12 towers).
///     Builder <c>B_BROADCAST_CHUGSOUNG_INFO(TOWER_INFO*)</c> (S05_MyTransfer.cpp:1552, serializes
///     <c>mState1Tower</c>/<c>mState2Tower</c>); emitters: <c>MyGame::SendTowerInfoLogic()</c>
///     (S07_MyGame01.cpp:13552, broadcast), <c>MyUtil::SendTowerInfo(user)</c> (S07_MyGame03.cpp:9716,
///     unicast), spawn-in inside <c>W_REGISTER_AVATAR_SEND</c> (S04_MyWork02.cpp:1203, unicast). The
///     sibling builder <c>B_BROADCAST_CHUGSOUNG_INFO2</c> (buffer C, l.1561) is never called.
///     <c>TOWER_INFO</c> itself is an internal server struct never serialized as-is — only the 2 × 12
///     ints are exposed on the wire.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BroadcastChugsoungInfo,
    ExpectedSize = 97)]
public readonly partial record struct ZcBroadcastChugsoungInfo : IOutgoingPacket
{
    [FixedArray(12)] public required int[] State1Tower { get; init; }
    [FixedArray(12)] public required int[] State2Tower { get; init; }
}
