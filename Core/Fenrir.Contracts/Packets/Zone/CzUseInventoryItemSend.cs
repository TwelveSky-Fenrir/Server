using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_USE_INVENTORY_ITEM_SEND (CLIENT.h:243-248) — <see cref="Value" /> is the quantity for mass box
///     opening (Shift+click). Responses: ZC 26 (result), ZC 119 (multi-item packs), ZC 194 (slot rewrite).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseInventoryItemSend,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzUseInventoryItemSend : IIncomingPacket<CzUseInventoryItemSend>
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }
}
