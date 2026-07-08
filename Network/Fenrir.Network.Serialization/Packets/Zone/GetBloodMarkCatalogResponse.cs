using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetBloodMarkCatalog,
    ExpectedSize = 605)]
public readonly partial record struct GetBloodMarkCatalogResponse : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
