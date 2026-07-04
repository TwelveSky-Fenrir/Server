using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Claim day is tracked server-side (0..6); empty payload.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ClaimDailyReward,
    ExpectedSize = 9, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ClaimDailyRewardRequest : IIncomingPacket<ClaimDailyRewardRequest>
{
}
