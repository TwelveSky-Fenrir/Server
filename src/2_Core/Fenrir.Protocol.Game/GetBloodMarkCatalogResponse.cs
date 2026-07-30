using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetBloodMarkCatalog,
    ExpectedSize = 605)]
public readonly partial record struct GetBloodMarkCatalogResponse : IOutgoingPacket
{
    public required BloodShop Data { get; init; }
}
