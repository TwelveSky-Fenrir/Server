using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     TRIBE_WORK_CASE6 (S04_MyWork02.cpp:10826-10830, local struct, pack(1)) — CZ_TRIBE_WORK_SEND tSort 6's
///     tData layout (USE_TITLE, doc 10 §2). Not a packet of its own, same treatment as
///     <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(8)]
public readonly partial record struct TribeWorkTitlePayload : IFenrirWireType<TribeWorkTitlePayload>
{
    /// <summary>1-14, the new title CATEGORY (combined with the current rank to form the new aTitle).</summary>
    public required int TitleSort { get; init; }

    /// <summary>Wire field carried but unused by the legacy formula (the new rank is derived from the CURRENT title's own rank, not this value) -- see doc 10 §2 tSort 6.</summary>
    public required int TitleLv { get; init; }
}
