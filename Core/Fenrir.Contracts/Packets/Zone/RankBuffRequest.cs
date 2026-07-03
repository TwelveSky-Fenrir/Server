using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_RANK_BUFF_SEND — 13 bytes (9 + 4). CORRECTION vs <c>01_cz_inventory.md</c> (which lists it as
///     empty, 9 bytes): CLIENT.h:345-348 makes <c>CZ_RANK_BUFF_SEND</c> an alias of the shared
///     <c>{ int tSort; }</c> typedef and the handler reads <c>r->tSort</c> (S04_MyWork02.cpp:14068);
///     <c>S_RANK_BUFF_SEND</c> (CLIENT.h:711) = 13. Handler <c>W_RANK_BUFF_SEND</c>
///     (S04_MyWork02.cpp:13994). <c>USE_RANK_POINT</c> is OFF so the compiled <c>#else</c> branch
///     (l.14066-14104) validates by tribe symbol-stone count (<c>ReturnSymbolNumNoMon</c>), not a no-op:
///     <c>IsMovingZone()</c> → <c>Quit()</c>; <c>mTribeSymbolBattle == 1</c> → silently ignored; tier
///     thresholds (tSort 1 → ≥1 stone, 2-3 → ≥2, 4-5 → ≥3, 6-7 → ≥4); tSort outside 1..7 → <c>Quit()</c>.
///     Reply: ZC 22 (<c>S068RANK_BUFF</c>, l.14110).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RankBuff, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct RankBuffRequest : IIncomingPacket<RankBuffRequest>
{
    public required int Sort { get; init; }
}
