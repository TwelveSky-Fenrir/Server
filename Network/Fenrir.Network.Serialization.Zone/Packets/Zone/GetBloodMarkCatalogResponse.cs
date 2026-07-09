using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetBloodMarkCatalog,
    ExpectedSize = 605)]
public readonly record struct GetBloodMarkCatalogResponse : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
