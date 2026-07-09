using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildMemberConnected,
    ExpectedSize = 14)]
public readonly record struct GuildMemberConnectedResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
