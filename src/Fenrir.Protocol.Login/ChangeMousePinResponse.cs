using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ChangeMousePin,
    ExpectedSize = 10)]
public readonly partial record struct ChangeMousePinResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(5)] public required string MousePassword { get; init; }
}
