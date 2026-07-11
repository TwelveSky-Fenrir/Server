using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct SkillUpgradeData : IFenrirWireType<SkillUpgradeData>
{
    public required int SkillIndex { get; init; }
}
