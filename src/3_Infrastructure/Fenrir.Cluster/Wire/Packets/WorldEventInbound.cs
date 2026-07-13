using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Cluster.Wire;

namespace Fenrir.Cluster.Wire.Packets;

[FenrirPacket(FenrirServer.Center, FenrirDirection.Incoming, Opcodes.Center.Incoming.WorldEvent, ExpectedSize = 135,
    AllowedStates = [(byte)CenterSessionState.Authenticated])]
public readonly partial record struct WorldEventInbound : IIncomingPacket<WorldEventInbound>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
