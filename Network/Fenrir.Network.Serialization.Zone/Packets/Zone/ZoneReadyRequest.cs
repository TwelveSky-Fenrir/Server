using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     CZ_CLIENT_OK_FOR_ZONE_SEND, opcode 13 -- the client's final ACK that it is ready for the zone it was
///     just registered into. <see cref="Tribe" /> feeds the tribe anti-tamper guardrail (the client must echo
///     back exactly the tribe already loaded server-side at registration); <see cref="AutoState" /> feeds the
///     auto-hunt anti-hack guardrail. See <c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneReadyService</c>
///     for the 3-guardrail business logic these fields drive.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1213-1291 (the 3 anti-cheat guardrails: heartbeat watchdog,
///     tribe anti-tamper, auto-hunt anti-hack). <see cref="AutoTime" />/<see cref="AutoTime2" /> are deserialized
///     but not currently consumed by any Fenrir guardrail -- not yet traced to a specific assignment/read site
///     in the cited legacy range, flagged as an open finding rather than assumed dead.
/// </remarks>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneReady,
    ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneReadyRequest : IIncomingPacket<ZoneReadyRequest>
{
    public required int Tribe { get; init; }
    public required int AutoTime { get; init; }
    public required int AutoTime2 { get; init; }
    public required int AutoState { get; init; }
}
