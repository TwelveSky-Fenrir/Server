using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmPetExperienceGrantPayload : IFenrirWireType<GmPetExperienceGrantPayload>
{

        public required int PetId { get; init; }

        public required int PetExperience { get; init; }
}
