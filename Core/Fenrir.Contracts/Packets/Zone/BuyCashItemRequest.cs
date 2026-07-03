using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_BUY_CASH_ITEM_SEND (CLIENT.h:349-356) — purchase a cash-shop item. <c>CostInfoIndex</c> indexes
///     <c>mCostInfoValue[MAX_CASH_NUM]</c>; <c>Version</c> must match <c>mCashInfo-&gt;mVersion</c>.
///     Runtime anti-spam: <c>Quit()</c> on two purchases &lt; 200 ms apart.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyCashItem, ExpectedSize = 49,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyCashItemRequest : IIncomingPacket<BuyCashItemRequest>
{
    public required int CostInfoIndex { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    public required int Version { get; init; }
}
