using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct SkillUpgradeData : IFenrirWireType<SkillUpgradeData>
{
    public required int SkillIndex { get; init; }
}
