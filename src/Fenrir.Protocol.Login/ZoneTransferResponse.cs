using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ZoneTransfer,
    ExpectedSize = 73)]
public readonly partial record struct ZoneTransferResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedString(16)] public required string Ip { get; init; }
    public required int Port { get; init; }
    public required int Zone { get; init; }
    [FixedString(44)] public required string Capability { get; init; }
}
