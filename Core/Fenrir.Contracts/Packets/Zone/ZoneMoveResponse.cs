using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DEMAND_ZONE_SERVER_INFO_2_RESULT (ZONE.h:470-475, 24-byte payload: 4 + 16 + 4, <c>_ip</c> = 16 per
///     DEFINE.h:405) — reply to CZ_DEMAND_ZONE_SERVER_INFO_2. Same <c>_ip</c> constant as the already-implemented
///     Login-side LC_DEMAND_ZONE_SERVER_INFO_RECV. On <c>Result=0</c> the legacy side sets
///     <c>mMoveZoneResult = 1</c> ("moving zone") on the session, which CZ_FAIL_MOVE_ZONE_2_SEND later clears.
///     Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneMove,
    ExpectedSize = 25)]
public readonly partial record struct ZoneMoveResponse : IOutgoingPacket
{
    /// <summary>0 = ok, client connects to Ip:Port; 1 = refused/zone unavailable.</summary>
    public required int Result { get; init; }

    [FixedString(16)] public required string Ip { get; init; }

    public required int Port { get; init; }
}
