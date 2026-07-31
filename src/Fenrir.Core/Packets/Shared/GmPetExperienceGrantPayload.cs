using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmPetExperienceGrantPayload : IFenrirWireType<GmPetExperienceGrantPayload>
{
    public required int PetId { get; init; }

    public required int PetExperience { get; init; }
}
