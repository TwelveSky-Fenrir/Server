using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_BUY_BLOOD_MARK_RECV (ZONE.h:1371-1378) — reply to CZ_BUY_BLOOD_MARK_SEND. <c>Result</c> 0 = ok,
///     1 = catalog unavailable (<c>mCashInfo</c> null), 2/3 = funds/inventory errors. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyBloodMarkItem, ExpectedSize = 41)]
public readonly partial record struct BuyBloodMarkItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int BloodCoin { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
