using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneMove,
    ExpectedSize = 25)]
public readonly partial record struct ZoneMoveResponse : IOutgoingPacket
{

        public required int Result { get; init; }

    [FixedString(16)] public required string Ip { get; init; }

    public required int Port { get; init; }
}
