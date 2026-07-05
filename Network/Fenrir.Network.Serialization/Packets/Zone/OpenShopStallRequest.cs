using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Sort: 1 = personal shop, 2 = proxy shop (else disconnect); proxy only enabled on zone 37 in EU33.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.OpenShopStall, ExpectedSize = 1245,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct OpenShopStallRequest : IIncomingPacket<OpenShopStallRequest>
{
    public required int Sort { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
