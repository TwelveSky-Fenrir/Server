using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_RETURN_TO_AUTO_ZONE (ZONE.h:942-944, header-only, 0-byte payload) — orders the client back to its
///     "automatic" zone (tribe capital). Emitted by the AFK-checker, failed zone transfers, and event-battle
///     endings (14 live call sites, S05_MyTransfer.cpp:1166-1179). Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ReturnToHomeZone, ExpectedSize = 1)]
public readonly partial record struct ReturnToHomeZoneResponse : IOutgoingPacket
{
}
