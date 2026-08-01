using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct TribeWorkSkillPayload : IFenrirWireType<TribeWorkSkillPayload>
{
    public required int TribeSkillSort { get; init; }
}
