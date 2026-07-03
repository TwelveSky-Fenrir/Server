using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_ADD_ITEM_RECV (ZONE.h:520-524) — reply to CZ_ADD_ITEM_SEND (25); unicast. The ONLY forge
///     response without a trailing <c>tValue[6]</c> (9 bytes total) — do not confuse with ZC 29/30/31 (33 bytes).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CombineItem, ExpectedSize = 9)]
public readonly partial record struct CombineItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }
}
