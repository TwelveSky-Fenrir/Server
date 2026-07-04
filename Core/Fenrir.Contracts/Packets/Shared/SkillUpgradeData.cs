using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     SKILL_UP_RECV (STRUCT.h:1244-1247, 4 bytes) — the re-read layer <c>MyWork::ProcessForData</c>
///     applies over CZ_PROCESS_DATA_SEND's <c>tData</c> blob (payload offset 4) for tSort 203
///     (<c>ProcessForSkillUpgrade</c>, V5 NPC &amp; Economy). Not a packet of its own, same treatment as
///     <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct SkillUpgradeData : IFenrirWireType<SkillUpgradeData>
{
    /// <summary>Legacy <c>tSkillIndex</c> -- the already-learned SLOT (0-39) to upgrade, NOT a skill id.</summary>
    public required int SkillIndex { get; init; }
}
