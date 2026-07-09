using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Empty payload; signals the client to re-fetch the cash catalog.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CashCatalogInvalidated,
    ExpectedSize = 1)]
public readonly record struct CashCatalogInvalidatedResponse : IOutgoingPacket
{
}
