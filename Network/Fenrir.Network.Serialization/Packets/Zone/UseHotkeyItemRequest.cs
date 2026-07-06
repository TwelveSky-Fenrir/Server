using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UseHotkeyItem, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct UseHotkeyItemRequest : IIncomingPacket<UseHotkeyItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }
}
