using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// USE_RANK_POINT is off: Sort (1-7) is validated against tribe symbol-stone count, not rank points.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RankBuff, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct RankBuffRequest : IIncomingPacket<RankBuffRequest>
{
    public required int Sort { get; init; }
}
