using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetBloodMarkCatalog,
    ExpectedSize = 605)]
public readonly partial record struct GetBloodMarkCatalogResponse : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
