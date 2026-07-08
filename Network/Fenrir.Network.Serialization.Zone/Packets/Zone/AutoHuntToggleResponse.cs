using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Also emitted when opening a personal shop stall, which forces AutoState = 0.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoHuntToggle, ExpectedSize = 13)]
public readonly partial record struct AutoHuntToggleResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int AutoState { get; init; }
}
