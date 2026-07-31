using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(1388)]
public readonly partial record struct GuildInfo : IFenrirWireType<GuildInfo>
{
    [FixedString(13)] public required string Name { get; init; }

    [Reserved(3)] public required int Grade { get; init; }

    [FixedString(13)] public required string Master { get; init; }

    [FixedString(13)] public required string SubMaster1 { get; init; }

    [FixedString(13)] public required string SubMaster2 { get; init; }

    [FixedArray(50)] [FixedString(13)] public required string[] MemberNames { get; init; }

    [Reserved(3)] [FixedArray(50)] public required int[] MemberRoles { get; init; }

    [FixedArray(50)] [FixedString(5)] public required string[] MemberCallNames { get; init; }

    [FixedArray(4)] [FixedString(51)] public required string[] Notices { get; init; }

    [Reserved(2)] public required int Point { get; init; }

    public required int BuffType { get; init; }

    public required int BuffState { get; init; }

    public required int BuffTime { get; init; }

    public required int ChangeLeader { get; init; }
}
