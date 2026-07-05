using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct TribeWorkSkillPayload : IFenrirWireType<TribeWorkSkillPayload>
{
    // 0-4; out of range aborts, matching the legacy's own Quit().
    public required int TribeSkillSort { get; init; }
}
