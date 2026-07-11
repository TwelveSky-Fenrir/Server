using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftLegendaryPet, ExpectedSize = 30)]
public readonly partial record struct CraftLegendaryPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

        public required byte Padding { get; init; }
}
