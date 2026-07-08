using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Value is the quantity for mass box opening (Shift+click).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseInventoryItem,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct UseInventoryItemRequest : IIncomingPacket<UseInventoryItemRequest>
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }
}
