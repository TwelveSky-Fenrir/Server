using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

// Re-read layer over CZ_PROCESS_DATA_SEND's tData for tSort 202/233 (NPC skill-offer lookups).
[FenrirWireType(8)]
public readonly partial record struct NpcSkillLearnData : IFenrirWireType<NpcSkillLearnData>
{
    public required int NpcId { get; init; }

    public required int SkillId { get; init; }
}
