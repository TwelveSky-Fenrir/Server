using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     May reply with 0, 1, or 2 of {HeroRankingPreviousResponse, HeroRankingCurrentResponse}, each gated by its own
///     2.5s throttle.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.HeroRanking, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct HeroRankingRequest : IIncomingPacket<HeroRankingRequest>
{
}
