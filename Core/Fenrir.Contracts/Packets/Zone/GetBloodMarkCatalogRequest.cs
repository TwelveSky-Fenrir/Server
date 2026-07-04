using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>No reply is sent if the character's mCashInfo hasn't loaded yet.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetBloodMarkCatalog,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetBloodMarkCatalogRequest : IIncomingPacket<GetBloodMarkCatalogRequest>
{
}
