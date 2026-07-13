using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DestroyItem, ExpectedSize = 33)]
public readonly partial record struct DestroyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Money { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
