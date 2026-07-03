using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     BUFF_INFO (STRUCT.h:1401-1404): <c>int aBuff[MAX_AVATAR_EFFECT_SORT_NUM][2]</c>, flattened row-major.
///     MAX_AVATAR_EFFECT_SORT_NUM (DEFINE.h:323-339) is a build-config switch, BUT <c>MG5ORIGIN</c> itself
///     is defined UNCONDITIONALLY at DEFINE.h:18 (a bare <c>#define</c>, not gated by <c>M33</c>/any other
///     macro) and <c>KR</c> is never defined anywhere in the tree -- so every build of this codebase,
///     ReleaseEU33 included, resolves to the <c>#elif defined MG5ORIGIN / #ifndef KR</c> branch = 35 slots
///     (70 ints), not the generic #else's 32. Confirmed by real compilation in
///     Docs/protocol/M1_Legacy_Wire_Contract.md §5/§7.2 (sizeof(AVATAR_INFO)=11168 sealed against cl.exe).
/// </summary>
[FenrirWireType(280)]
public readonly partial record struct BuffInfo : IFenrirWireType<BuffInfo>
{
    [FixedArray(70)] public required int[] Buff { get; init; }
}
