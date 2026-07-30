using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffRegister,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffRegisterResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}
