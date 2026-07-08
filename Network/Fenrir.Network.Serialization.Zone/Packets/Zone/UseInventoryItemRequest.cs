using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Value is the quantity for mass box opening (Shift+click).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseInventoryItem,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseInventoryItemRequest : IIncomingPacket<UseInventoryItemRequest>
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }
}
