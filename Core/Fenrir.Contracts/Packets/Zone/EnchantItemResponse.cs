using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_IMPROVE_ITEM_RECV (ZONE.h:513-518) — reply to CZ_IMPROVE_ITEM_SEND (24); unicast. Result codes:
///     0 success, 1 failure/-1, 2 destroyed, 3 reset-to-+40, 4 protected, 8/9 LNW33 scroll failures, 999
///     special cases. <see cref="Value" /> is spelled <c>iValue</c> in the source (inconsistent prefix) —
///     it is a genuine packet member, not a nested struct.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnchantItem, ExpectedSize = 13)]
public readonly partial record struct EnchantItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    public required int Value { get; init; }
}
