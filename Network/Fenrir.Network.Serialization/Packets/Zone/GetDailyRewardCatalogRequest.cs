using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>IPC failure, or a false result from ts25extra, disconnects the client.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetDailyRewardCatalog,
    ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetDailyRewardCatalogRequest : IIncomingPacket<GetDailyRewardCatalogRequest>
{
}
