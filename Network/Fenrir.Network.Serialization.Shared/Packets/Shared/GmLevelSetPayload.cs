using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 521, the GM "Basic"-tier (legacy <c>uUserSort &gt;= 1</c>) self level/experience-tier-set command
///     (Server/ts25zone/S04_MyWork04.cpp:1541-1596). There is no dedicated legacy wire opcode for this command
///     -- it is multiplexed inside the same generic envelope every other <see cref="GenericActionRequest" />
///     tSort uses, so this is an embedded payload (<see cref="IFenrirWireType{TSelf}" />), never a standalone
///     <c>[FenrirPacket]</c>. No target-character parameter exists in this payload -- this command can only
///     ever change the invoking GM's own character. Notably a lower gate threshold than the sibling EXP command
///     (tSort 503, tier &gt;= 10) -- this type only covers tSort 521.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:1541-1596 (case 521 body: gate, inert per-account flag check
///     -- dead branch, never enforced, not reproduced by this type --, three-tier decomposition, life/mana
///     full-reset side effect, absence of any audit-log call) ; Server/Header/Protocol/STRUCT.h:1282-1284 (this
///     request struct's shape -- single whole number, distinct from the locally-shadowed same-name type the
///     sibling tSort 522 STR/DEX/VIT/INT command inertly declares but never reads) ; Server/Header/Protocol/
///     DEFINE.h:604-605 (tier-boundary constants: base cap 145, high-level span 12 -- used by the caller to
///     decompose this value into base/high-level/rebirth tiers, not by this type itself).
/// </remarks>
[FenrirWireType(4)]
public readonly partial record struct GmLevelSetPayload : IFenrirWireType<GmLevelSetPayload>
{
    /// <summary>
    ///     Requested total level, spanning three successive tiers combined into one number: base level (up
    ///     to 145), then "high level" (a further 12), then rebirth count (a further 12) -- 169 total capacity. No
    ///     lower bound is enforced by the legacy code (every comparison is "less than or equal", so a zero/negative
    ///     value reaches the base tier's branch unchanged and is applied as-is); a value above 169 is rejected with
    ///     no mutation. Decomposed/validated by the caller, not here.
    /// </summary>
    public required int Level { get; init; }
}
