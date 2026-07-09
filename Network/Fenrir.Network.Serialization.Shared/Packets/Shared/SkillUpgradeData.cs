using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

// Re-read layer over CZ_PROCESS_DATA_SEND's tData for tSort 203 (skill upgrade).
[FenrirWireType(4)]
public readonly record struct SkillUpgradeData : IFenrirWireType<SkillUpgradeData>
{
    // The already-learned SLOT (0-39) to upgrade, NOT a skill id.
    public required int SkillIndex { get; init; }
}
