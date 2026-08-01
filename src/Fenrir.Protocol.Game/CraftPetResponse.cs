using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftPet, ExpectedSize = 29)]
public readonly partial record struct CraftPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
