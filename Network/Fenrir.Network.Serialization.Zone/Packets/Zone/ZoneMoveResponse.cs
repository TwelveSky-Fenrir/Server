using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Result=0 sets the session's "moving zone" flag, later cleared by <see cref="ZoneTransferCancelRequest" />.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneMove,
    ExpectedSize = 25)]
public readonly record struct ZoneMoveResponse : IOutgoingPacket
{
    /// <summary>0 = ok, client connects to Ip:Port; 1 = refused/zone unavailable.</summary>
    public required int Result { get; init; }

    [FixedString(16)] public required string Ip { get; init; }

    public required int Port { get; init; }
}
