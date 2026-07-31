using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Zone088TypeInfo,
    ExpectedSize = 9)]
public readonly partial record struct Zone088TypeInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
