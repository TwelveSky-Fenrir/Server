using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// One request yields a burst of SearchShopListingsResponse packets, one per matching listing.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SearchShopListings, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct SearchShopListingsRequest : IIncomingPacket<SearchShopListingsRequest>
{
    public required int Sort1 { get; init; }
    public required int Sort2 { get; init; }
}
