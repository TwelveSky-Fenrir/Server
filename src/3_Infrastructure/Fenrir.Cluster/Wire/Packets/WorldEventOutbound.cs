using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Cluster.Wire.Packets;

[FenrirPacket(FenrirServer.Center, FenrirDirection.Outgoing, Opcodes.Center.Outgoing.WorldEvent, ExpectedSize = 135)]
public readonly partial record struct WorldEventOutbound : IOutgoingPacket
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
