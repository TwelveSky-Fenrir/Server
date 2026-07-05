using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(2456)]
public readonly partial record struct TribeInfo : IFenrirWireType<TribeInfo>
{
    // Flattened row-major char[4][10][13]: 40 rows of 13 bytes.
    [FixedArray(40)] [FixedString(13)] public required string[] TribeVoteName { get; init; }

    // Flattened row-major int[4][10] (40 total).
    [FixedArray(40)] public required int[] TribeVoteLevel { get; init; }

    [FixedArray(40)] public required int[] TribeVoteKillOtherTribe { get; init; }
    [FixedArray(40)] public required int[] TribeVotePoint { get; init; }

    [FixedArray(4)] [FixedString(13)] public required string[] TribeMaster { get; init; }

    // Flattened row-major char[4][12][13]: 48 rows of 13 bytes.
    [FixedArray(48)] [FixedString(13)] public required string[] TribeSubMaster { get; init; }

    // Flattened row-major char[4][5][13]: 20 rows of 13 bytes.
    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName1 { get; init; }

    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName2 { get; init; }
    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName3 { get; init; }
}
