using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CashCatalogInvalidated,
    ExpectedSize = 1)]
public readonly partial record struct CashCatalogInvalidatedResponse : IOutgoingPacket
{
}
