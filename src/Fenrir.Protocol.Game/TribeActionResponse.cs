using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAction, ExpectedSize = 109)]
public readonly partial record struct TribeActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
