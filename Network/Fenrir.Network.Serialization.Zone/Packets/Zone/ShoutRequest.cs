using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Shout, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ShoutRequest : IIncomingPacket<ShoutRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
