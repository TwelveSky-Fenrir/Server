using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GET_REWARD_ITEM_SEND (CLIENT.h:160) — request the 7-day login reward catalog. Empty payload
///     (header only). IPC failure or a false <c>mRecv_Result</c> from ts25extra disconnects the client
///     (aggressive). Reply: ZC_GET_REWARD_ITEM_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GetDailyRewardCatalog, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GetDailyRewardCatalogRequest : IIncomingPacket<GetDailyRewardCatalogRequest>
{
}
