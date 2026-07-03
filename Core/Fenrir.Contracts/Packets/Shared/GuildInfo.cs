using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     GUILD_INFO (STRUCT.h:684-700, 1388 bytes) — full guild snapshot carried whole (padding included,
///     <c>memcpy</c> via <c>write2_dst_ptr</c>) by ZC_GUILD_WORK_RECV (ZC 83). Declared outside
///     STRUCT.h's <c>pack(1)</c> regions → natural MSVC x86 alignment inserts THREE padding zones that
///     are part of the wire layout: 3 bytes after <see cref="Name" /> (char[13], before the int
///     <see cref="Grade" />), 3 bytes after <see cref="MemberNames" /> (char[50][13], before the int
///     array <see cref="MemberRoles" />), and 2 bytes after <see cref="Notices" /> (char[4][51], before
///     the int <see cref="Point" />). <c>sizeof</c> = 1388, a multiple of 4, so there is no trailing
///     padding after <see cref="ChangeLeader" />.
/// </summary>
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
