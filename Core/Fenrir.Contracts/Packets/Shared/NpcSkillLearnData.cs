using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     NPC_SKILL_RECV (STRUCT.h:1240-1243, 8 bytes) — the re-read layer <c>MyWork::ProcessForData</c>
///     applies over CZ_PROCESS_DATA_SEND's <c>tData</c> blob (payload offset 4) for tSort 202
///     (<c>ProcessForLearnSkill1</c>) and 233 (<c>ProcessForLearnSkill2</c>) — same layout for both, only
///     which of the NPC's two skill-offer arrays gets searched differs (report 04_mega_switches.md §1,
///     V5 NPC &amp; Economy). Not a packet of its own, same treatment as <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(8)]
public readonly partial record struct NpcSkillLearnData : IFenrirWireType<NpcSkillLearnData>
{
    /// <summary>Legacy <c>nIndex</c> -- the NPC teaching this skill.</summary>
    public required int NpcId { get; init; }

    /// <summary>Legacy <c>sIndex</c> -- the requested skill's catalog id.</summary>
    public required int SkillId { get; init; }
}
