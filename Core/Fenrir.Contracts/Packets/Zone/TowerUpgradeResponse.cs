using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     Result: 0=OK, 1=missing herb(666), 2=missing bar(1073). On success both consumptions are packed
///     into slot 0: Page[0] = herbPage + 10000 + barPage*100, Index[0] mirrors it; slot 1 unused.
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
