using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneMove,
    ExpectedSize = 69)]
public readonly partial record struct ZoneMoveResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(16)] public required string Ip { get; init; }

    public required int Port { get; init; }

    [FixedString(44)] public required string Capability { get; init; }
}
