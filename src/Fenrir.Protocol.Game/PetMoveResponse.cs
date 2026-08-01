using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PetMove, ExpectedSize = 17)]
public readonly partial record struct PetMoveResponse : IOutgoingPacket
{
    [FixedArray(3)] public required float[] Location { get; init; }

    public required float Frame { get; init; }
}
