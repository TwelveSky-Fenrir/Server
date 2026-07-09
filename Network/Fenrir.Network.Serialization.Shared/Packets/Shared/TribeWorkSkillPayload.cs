using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly record struct TribeWorkSkillPayload : IFenrirWireType<TribeWorkSkillPayload>
{
    // 0-4; out of range aborts, matching the legacy's own Quit().
    public required int TribeSkillSort { get; init; }
}
