using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(2456)]
public readonly partial record struct TribeInfo : IFenrirWireType<TribeInfo>
{
    [FixedArray(40)] [FixedString(13)] public required string[] TribeVoteName { get; init; }

    [FixedArray(40)] public required int[] TribeVoteLevel { get; init; }

    [FixedArray(40)] public required int[] TribeVoteKillOtherTribe { get; init; }
    [FixedArray(40)] public required int[] TribeVotePoint { get; init; }

    [FixedArray(4)] [FixedString(13)] public required string[] TribeMaster { get; init; }

    [FixedArray(48)] [FixedString(13)] public required string[] TribeSubMaster { get; init; }

    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName1 { get; init; }

    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName2 { get; init; }
    [FixedArray(20)] [FixedString(13)] public required string[] HoisundoName3 { get; init; }
}
