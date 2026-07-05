using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ZoneTransfer,
    ExpectedSize = 29)]
public readonly partial record struct ZoneTransferResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedString(16)] public required string Ip { get; init; }
    public required int Port { get; init; }
    public required int Zone { get; init; }
}
