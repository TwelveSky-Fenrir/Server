using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_HERORANK_INFO_RECV (ZONE.h:1204-1208) — builder <c>B_HERORANK_INFO_RECV</c>
///     (S05_MyTransfer.cpp:1513); reply to CZ 118 (S04_MyWork02.cpp:14168): the PREVIOUS ranking
///     (<c>mRankInfo->mPrevious</c>), throttled 2.5s per user (<c>mTickForRankingPre</c>). Shares its
///     C++ typedef with ZC 150 (same layout, dedicated C# record per spec).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRankingPrevious,
    ExpectedSize = 685)]
public readonly partial record struct HeroRankingPreviousResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required HeroRank HeroInfo { get; init; }
}
