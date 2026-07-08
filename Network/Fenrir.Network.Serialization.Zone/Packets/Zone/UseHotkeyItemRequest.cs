using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseHotkeyItem, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseHotkeyItemRequest : IIncomingPacket<UseHotkeyItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }
}
