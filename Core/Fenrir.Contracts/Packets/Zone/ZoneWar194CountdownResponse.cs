using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_194_TYPE_BATTLE_COUNTDOWN (ZONE.h:958-961) — startup countdown for the "194" battle
///     (S07_MyGame01.cpp:10254/10260, tick 1 then every 10 ticks, value <c>60 - tick/2</c>),
///     <c>BroadcastServer(1)</c>. ZONE.h intercalates macro <c>ZCP_FFA_TYPE_BATTLE_INFO 200</c> between
///     the 100 and 101 defines (l.1589-1593) — resolved by define value, not file order.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar194Countdown,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar194CountdownResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
