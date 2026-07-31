using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DestroyItem, ExpectedSize = 33)]
public readonly partial record struct DestroyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Money { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
