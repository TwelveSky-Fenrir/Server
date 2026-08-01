using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SocketSlotInsert,
    ExpectedSize = 17)]
public readonly partial record struct SocketSlotInsertResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(3)] public required int[] Value { get; init; }
}
