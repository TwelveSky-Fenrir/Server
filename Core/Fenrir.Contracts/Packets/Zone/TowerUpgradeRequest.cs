using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_CHUGSOUNG_WAR_UP_SEND (CLIENT.h:479-484, <c>S_CHUGSOUNG_WAR_UP_SEND</c> CLIENT.h:729) —
///     handler <c>W_CHUGSOUNG_WAR_UP_SEND</c> (S04_MyWork02.cpp:14268), entire body under
///     <c>#ifdef USE_TOWER</c> (ON). All validations sanctioned by <c>Quit()</c>
///     (l.14279-14401): sender is neither tribe chief nor sub-chief (<c>ReturnTribeRole</c> ∉ {1,2});
///     invalid tower (<c>mTowerValid</c> false / <c>GetTowerID() == -1</c>); <see cref="Index" /> !=
///     <c>mServerNumber</c>; tower index outside the tribe's range
///     (<c>[aTribe*3, aTribe*3+3)</c>); <see cref="Value01" /> outside 1..3; <see cref="Value02" /> not
///     in {2,4,6}; type/level mismatch with server state; missing materials (items 666, 1073). Success:
///     consumes 1 Mystic Bronze Herb + 1 Silver Bar, raises <c>mTowerState</c>, replies ZC 151.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TowerUpgrade,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TowerUpgradeRequest : IIncomingPacket<TowerUpgradeRequest>
{
    public required int Index { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
