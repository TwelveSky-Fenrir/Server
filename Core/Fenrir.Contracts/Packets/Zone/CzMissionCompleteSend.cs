using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_MISSION_COMPLETE_SEND (CLIENT.h:345-348, same shared <c>{ int tSort; }</c> typedef as CZ
///     97/111, <c>S_MISSION_COMPLETE_SEND</c> CLIENT.h:733) — handler
///     <c>W_MISSION_COMPLETE_SEND</c> (S04_MyWork02.cpp:14521). <c>tSort</c> must be 1 (view counters)
///     or 2 (claim daily reward), else <c>Quit()</c>. For <c>tSort = 2</c>: level below <c>LV_M1</c> →
///     <c>Quit()</c>; <c>aJoinWar &lt; 1</c> or <c>aKillOtherTribe &lt; 10</c> → <c>Quit()</c> (the
///     <c>aKillMonster</c>/<c>aPlayTime</c> guards are gated by <c>USE_DAILY_UI_MONSTER_KILL</c>, OFF →
///     not compiled); missing reward item → <c>Quit()</c>; full inventory → <c>Result = 3</c> without
///     <c>Quit()</c>. Success: deducts counters, delivers the item via ZC 119 (MULTI_ITEM_CREATE,
///     <c>tNum = 7001 + iCount</c>) and, at max level, increments <c>aZone241Time</c> (ZC 22, sort 200).
///     Always finishes with ZC 163.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MissionCompleteSend,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzMissionCompleteSend : IIncomingPacket<CzMissionCompleteSend>
{
    public required int Sort { get; init; }
}
