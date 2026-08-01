using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetBloodMarkCatalog,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetBloodMarkCatalogRequest : IIncomingPacket<GetBloodMarkCatalogRequest>
{
}
