using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SystemMessage,
    ExpectedSize = 5)]
public readonly partial record struct SystemMessageResponse : IOutgoingPacket
{
    public required int MessageId { get; init; }
}
