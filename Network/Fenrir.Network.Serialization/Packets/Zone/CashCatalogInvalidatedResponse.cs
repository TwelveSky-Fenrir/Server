using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Empty payload; signals the client to re-fetch the cash catalog.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CashCatalogInvalidated,
    ExpectedSize = 1)]
public readonly partial record struct CashCatalogInvalidatedResponse : IOutgoingPacket
{
}
