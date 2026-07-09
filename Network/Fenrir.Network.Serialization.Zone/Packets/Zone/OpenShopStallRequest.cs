using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Sort: 1 = personal shop, 2 = proxy shop (else disconnect); proxy only enabled on zone 37 in EU33.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.OpenShopStall, ExpectedSize = 1245,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct OpenShopStallRequest : IIncomingPacket<OpenShopStallRequest>
{
    public required int Sort { get; init; }
    public required PshopInfo PshopInfo { get; init; }
}
