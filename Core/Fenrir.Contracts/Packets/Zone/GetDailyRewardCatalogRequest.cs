using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>IPC failure, or a false result from ts25extra, disconnects the client.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetDailyRewardCatalog,
    ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetDailyRewardCatalogRequest : IIncomingPacket<GetDailyRewardCatalogRequest>
{
}
