using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_USE_INVENTORY_ITEM_RECV (ZONE.h:484-493) — reply to CZ_USE_INVENTORY_ITEM_SEND (23); unicast.
///     <see cref="Value2" /> is present (<c>USE_PREMIUM_LONGTIME</c>, active in EU33, DEFINE.h:63): the
///     Y2038 double-encoded premium date. A build without <c>USE_PREMIUM_LONGTIME</c> would be 17 bytes
///     total — locked to 21 here.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UseInventoryItem,
    ExpectedSize = 21)]
public readonly partial record struct UseInventoryItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }

    public required int Value2 { get; init; }
}
