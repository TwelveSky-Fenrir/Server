using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(104)]
public readonly partial record struct ActionInfo : IFenrirWireType<ActionInfo>
{
    public required int Type { get; init; }
    public required int Sort { get; init; }
    public required float Frame { get; init; }
    [FixedArray(3)] public required float[] Location { get; init; }
    [FixedArray(3)] public required float[] TargetLocation { get; init; }
    public required float Front { get; init; }
    public required float TargetFront { get; init; }
    [FixedArray(3)] public required float[] PetLocation { get; init; }
    [FixedArray(3)] public required float[] PetTargetLocation { get; init; }
    public required float PetFront { get; init; }
    public required int PetSort { get; init; }
    public required int TargetObjectSort { get; init; }
    public required int TargetObjectIndex { get; init; }
    public required int TargetObjectUniqueNumber { get; init; }
    public required int SkillNumber { get; init; }
    public required int SkillGradeNum1 { get; init; }
    public required int SkillGradeNum2 { get; init; }
    public required int SkillValue { get; init; }
}
