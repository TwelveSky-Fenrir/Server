using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     TRIBE_WORK_CASE5 (S04_MyWork02.cpp:10822-10825, local struct, pack(1)) — CZ_TRIBE_WORK_SEND tSort 5's
///     tData layout (doc 10 §2). Not a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct TribeWorkSkillPayload : IFenrirWireType<TribeWorkSkillPayload>
{
    /// <summary>0-4 (validity checked by the caller -- out of range aborts, matching the legacy's own <c>Quit()</c>).</summary>
    public required int TribeSkillSort { get; init; }
}
