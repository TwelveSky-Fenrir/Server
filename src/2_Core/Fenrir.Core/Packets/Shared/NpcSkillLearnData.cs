using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct NpcSkillLearnData : IFenrirWireType<NpcSkillLearnData>
{
    public required int NpcId { get; init; }

    public required int SkillId { get; init; }
}
