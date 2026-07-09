using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     Result is always 0 in the traced source: <c>FindInventoryItem</c> (S04_MyWork05.cpp:4867) never
///     returns a hit with a -1 page/index, so the handler's own "missing herb(666)=1 / missing bar(1073)=2"
///     branches (S04_MyWork02.cpp:14396-14403) are dead code -- a missing herb or bar disconnects instead.
///     On success both consumptions are packed into slot 0: Page[0] = herbPage + 10000 + barPage*100,
///     Index[0] mirrors it; slot 1 unused.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerUpgrade,
    ExpectedSize = 25)]
public readonly record struct TowerUpgradeResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(2)] public required int[] Page { get; init; }
    [FixedArray(2)] public required int[] Index { get; init; }
    public required int Count { get; init; }
}
