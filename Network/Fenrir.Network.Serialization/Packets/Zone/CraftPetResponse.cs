using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Result: 0=success (recipes 4-6), 10000=success (recipes 1-3, e.g. consolation pet 92291). Value[0]=pet item index.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftPet, ExpectedSize = 29)]
public readonly partial record struct CraftPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
