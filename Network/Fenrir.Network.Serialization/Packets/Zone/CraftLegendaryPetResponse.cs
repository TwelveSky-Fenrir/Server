using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Wire size is 30 (dead trailing Padding byte), not 29.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftLegendaryPet, ExpectedSize = 30)]
public readonly partial record struct CraftLegendaryPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

    /// <summary>Dead trailing byte (<c>BYTE tmp</c>), always written as 0.</summary>
    public required byte Padding { get; init; }
}
