using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     May reply with 0, 1, or 2 of {HeroRankingPreviousResponse, HeroRankingCurrentResponse}, each gated by its own
///     2.5s throttle.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.HeroRanking, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct HeroRankingRequest : IIncomingPacket<HeroRankingRequest>
{
}
