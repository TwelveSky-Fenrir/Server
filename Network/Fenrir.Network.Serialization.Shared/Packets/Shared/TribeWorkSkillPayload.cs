using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct TribeWorkSkillPayload : IFenrirWireType<TribeWorkSkillPayload>
{
    public required int TribeSkillSort { get; init; }
}
