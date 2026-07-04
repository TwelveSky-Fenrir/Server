using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Value is the quantity for mass box opening (Shift+click).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseInventoryItem,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseInventoryItemRequest : IIncomingPacket<UseInventoryItemRequest>
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }
}
