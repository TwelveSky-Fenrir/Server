using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ViewShopStall, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ViewShopStallRequest : IIncomingPacket<ViewShopStallRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
