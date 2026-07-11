using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SearchShopListings, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct SearchShopListingsRequest : IIncomingPacket<SearchShopListingsRequest>
{
    public required int Sort1 { get; init; }
    public required int Sort2 { get; init; }
}
