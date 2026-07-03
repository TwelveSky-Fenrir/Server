using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_CLAIM_REWARD_ITEM_SEND (CLIENT.h:160) — claim today's login reward for
///     <c>uRewardClaimDay</c> (0..6; out of range or unknown item -&gt; Quit()). Empty payload
///     (header only). Reply: ZC_CLAIM_REWARD_ITEM_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ClaimRewardItemSend,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzClaimRewardItemSend : IIncomingPacket<CzClaimRewardItemSend>
{
}
