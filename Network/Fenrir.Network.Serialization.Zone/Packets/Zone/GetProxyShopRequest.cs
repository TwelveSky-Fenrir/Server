using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetProxyShop,
    ExpectedSize = 30, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetProxyShopRequest : IIncomingPacket<GetProxyShopRequest>
{
    public required int Sort { get; init; }
    public required int UniqueNumber { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
