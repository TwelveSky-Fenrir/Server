using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_DEMAND_ZONE_SERVER_INFO_2 (CLIENT.h:221-226, 12-byte payload) — zone-transfer request, handled by
///     <c>W_DEMAND_ZONE_SERVER_INFO_2</c>. Legacy validation (S04_MyWork02.cpp:2065-2114): <c>tZoneNumber</c> equal
///     to the current zone is ignored; <c>tZoneNumber &lt; 1 || &gt;= 350</c> or <c>tPresentZoneNumber</c>
///     mismatching the current zone -&gt; <c>Quit()</c>; <c>tSort == 2</c> (GM transfer) requires
///     <c>uUserSort &gt;= 1</c> else <c>Quit()</c>; <c>tSort</c> outside 2..12 -&gt; <c>Quit()</c>. Replies with
///     ZC_DEMAND_ZONE_SERVER_INFO_2_RESULT; on success the session enters "moving zone"
///     (<c>mMoveZoneResult = 1</c>, <c>mRegisterTime</c> re-stamped) — model as a Fenrir session flag, not a
///     <see cref="ZoneSessionState" />.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneMove,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneMoveRequest : IIncomingPacket<ZoneMoveRequest>
{
    /// <summary>
    ///     Transfer reason: 2=GM, 3=death, 4=portal, 5=paying NPC, 6=NPC move, 7=return, 8=return item, 9=teleport
    ///     item, 10=NPC, 11=auto-&gt;zone 37, 12=zone 84.
    /// </summary>
    public required int Sort { get; init; }

    public required int ZoneNumber { get; init; }
    public required int PresentZoneNumber { get; init; }
}
