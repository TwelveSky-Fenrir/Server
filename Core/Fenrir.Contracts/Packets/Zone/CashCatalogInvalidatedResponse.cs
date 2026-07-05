using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Empty payload; signals the client to re-fetch the cash catalog.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CashCatalogInvalidated,
    ExpectedSize = 1)]
public readonly partial record struct CashCatalogInvalidatedResponse : IOutgoingPacket
{
}
