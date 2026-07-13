using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetBloodMarkCatalog,
    ExpectedSize = 605)]
public readonly partial record struct GetBloodMarkCatalogResponse : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
