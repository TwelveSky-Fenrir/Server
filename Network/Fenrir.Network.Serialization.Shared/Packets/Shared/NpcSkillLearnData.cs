using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct NpcSkillLearnData : IFenrirWireType<NpcSkillLearnData>
{
    public required int NpcId { get; init; }

    public required int SkillId { get; init; }
}
