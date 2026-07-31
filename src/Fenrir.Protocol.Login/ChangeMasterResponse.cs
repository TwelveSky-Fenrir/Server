using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ChangeMaster,
    ExpectedSize = 5)]
public readonly partial record struct ChangeMasterResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
