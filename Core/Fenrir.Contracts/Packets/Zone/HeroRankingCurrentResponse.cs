using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_LASTHERORANK_INFO_RECV (ZONE.h:1204-1208, typedef shared with ZC 148) — builder
///     <c>B_LASTHERORANK_INFO_RECV</c> (S05_MyTransfer.cpp:1536); same CZ 118 handler
///     (S04_MyWork02.cpp:14174): the CURRENT ranking (<c>mRankInfo->mCurrent</c>), throttled 2.5s
///     (<c>mTickForRankingCur</c>). One CZ 118 request can yield 0, 1 or 2 packets (148 and/or 150)
///     depending on their respective throttles.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRankingCurrent,
    ExpectedSize = 685)]
public readonly partial record struct HeroRankingCurrentResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required HeroRank HeroInfo { get; init; }
}
