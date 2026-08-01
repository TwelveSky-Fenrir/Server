using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SocketSystem,
    ExpectedSize = 21)]
public readonly partial record struct SocketSystemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Sort { get; init; }

    [FixedArray(3)] public required int[] Value { get; init; }
}
