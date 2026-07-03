using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_CHUGSOUNG_WAR_UP_RECV (ZONE.h:1220-1226) — builder <c>B_CHUGSOUNG_WAR_UP_RECV</c>
///     (S05_MyTransfer.cpp:1543); reply to CZ 120 (S04_MyWork02.cpp:14427).
///     <see cref="Result" />: 0 = OK, 1 = missing Mystic Bronze Herb (item 666), 2 = missing Silver Bar
///     (item 1073). On success: <c>Page[0] = herbPage + 10000 + barPage*100</c>, <c>Index[0]</c> mirrors
///     it, <c>Page[1] = Index[1] = 0</c> (S04_MyWork02.cpp:14416-14420) — two inventory consumptions
///     compacted into slot 0.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerUpgrade,
    ExpectedSize = 25)]
public readonly partial record struct TowerUpgradeResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(2)] public required int[] Page { get; init; }
    [FixedArray(2)] public required int[] Index { get; init; }
    public required int Count { get; init; }
}
