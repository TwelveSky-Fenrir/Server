using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ZoneTransfer,
    ExpectedSize = 29)]
public readonly partial record struct ZoneTransferResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedString(16)] public required string Ip { get; init; }
    public required int Port { get; init; }
    public required int Zone { get; init; }
}
