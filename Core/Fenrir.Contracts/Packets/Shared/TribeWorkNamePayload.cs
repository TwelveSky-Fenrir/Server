using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     TRIBE_WORK_CASE2/CASE3 (S04_MyWork02.cpp:10817-10821, local structs, pack(1)) — CZ_TRIBE_WORK_SEND
///     tSort 2 (appoint sub-master) and tSort 3 (remove sub-master)'s shared tData layout (doc 10 §2). Not
///     a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(13)]
public readonly partial record struct TribeWorkNamePayload : IFenrirWireType<TribeWorkNamePayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
