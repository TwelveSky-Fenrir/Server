using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftLegendaryPet, ExpectedSize = 30)]
public readonly partial record struct CraftLegendaryPetResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

    public required byte Padding { get; init; }
}
