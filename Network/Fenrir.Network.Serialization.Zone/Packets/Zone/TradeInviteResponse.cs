using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeInvite, ExpectedSize = 18)]
public readonly record struct TradeInviteResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Level { get; init; }
}
