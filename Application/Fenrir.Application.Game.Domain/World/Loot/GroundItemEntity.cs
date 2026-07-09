using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     One item lying on the ground in a <see cref="Zone" /> -- immutable snapshot twin of legacy
///     <c>ITEM_OBJECT</c>. Never mutated after drop; pickup is a
///     <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" /> atomic remove, the one
///     exception to the tick-owned zone-state rule (see <see cref="Zone.TryClaimGroundItem" />).
/// </summary>
public sealed record GroundItemEntity(
    int ServerIndex,
    uint UniqueNumber,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    float PosX,
    float PosY,
    float PosZ,
    string Master,
    string PartyName,
    int DropSort,
    TimeSpan CreatedAtZoneClock,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    // Zone-241 "LOD" personal-dungeon tag (legacy ITEM_OBJECT's own instance-id field, stamped from the
    // killing avatar's own PlayerRuntimeState.DungeonInstanceId at drop time) -- null for every ordinary
    // drop. See Zone.DungeonInstance.cs's remarks for the broadcast/pickup filters keyed on this field.
    int? InstanceId = null)
{
    /// <summary>
    ///     Monster-kill drop-sort code (legacy <c>DP_MN_TO_WD</c>, "monster to world") -- the value
    ///     <see cref="Fenrir.Application.Game.Domain.World.Monsters.MonsterSpawnScheduler" />'s
    ///     <c>ResolvePartyDrop</c> already stamps on a partied killer's loot.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:604-631 ; Server/Header/Protocol/DEFINE.h:517-520.</remarks>
    public const int MonsterKillDropSort = 1;

    /// <summary>
    ///     Player-manual-ground-drop code (legacy <c>DP_IV_TO_WD</c>, "inventory to world"). No Fenrir spawn
    ///     path stamps this value yet -- the "drop item from inventory to ground" player action has no
    ///     ground-item producer in this codebase today (only <c>MonsterSpawnScheduler</c> ever calls
    ///     <see cref="Zone.SpawnGroundItem" /> with a non-zero <see cref="DropSort" />), so the rule-6 branch
    ///     below is presently unreachable in practice, not merely untested.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/Protocol/DEFINE.h:517-520 names both this code and
    ///     <see cref="MonsterKillDropSort" /> adjacently, but this specific numeric value was not
    ///     independently re-confirmed against the header for this change -- it is inferred by adjacency to the
    ///     already-established <see cref="MonsterKillDropSort" />, not re-derived. Re-verify against
    ///     DEFINE.h:517-520 before wiring an actual manual drop-to-ground producer to this constant.
    /// </remarks>
    public const int ManualGroundDropSort = 2;

    /// <summary>
    ///     GM-originated world drop code, stamped by the Admin-tier (<c>GmCommandTier.Admin</c>) spawn-item
    ///     command (CZ_PROCESS_DATA_SEND tSort 505/523) via
    ///     <see cref="Fenrir.Application.Game.Services.Gm.GmCreateItemService" />. Never gates any of
    ///     <see cref="IsClaimableBy" />'s ownership rules today (rules 5/6 only special-case
    ///     <see cref="MonsterKillDropSort" />/<see cref="ManualGroundDropSort" />), so a GM-created item falls
    ///     through to the same rule-2/3/4 (free-for-all-once-expired, no-owner-free-for-anyone,
    ///     owner-can-reclaim) treatment as any other <see cref="Master" />-stamped drop.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/Protocol/DEFINE.h:520 names this specific GM-drop-source code, distinct
    ///     from <see cref="MonsterKillDropSort" /> (:517-520 broadly) and <see cref="ManualGroundDropSort" />.
    ///     Its concrete numeric value was NOT independently confirmed against the header for this change --
    ///     same "inferred by adjacency, not re-derived" posture <see cref="ManualGroundDropSort" /> already
    ///     documents for itself, extended one step further here (next unused value after 1/2). Re-verify
    ///     against DEFINE.h:517-520 (the small drop-source-code block that range spans) before relying on this
    ///     exact value for anything beyond the audit-trail/informational tagging
    ///     <see cref="Fenrir.Application.Game.Services.Gm.GmCreateItemService" /> uses it for today.
    /// </remarks>
    public const int GmCreateItemDropSort = 3;

    public bool IsExpired(TimeSpan nowZoneClock)
    {
        return nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemLifetime;
    }

    /// <summary>
    ///     Ports <c>ITEM_OBJECT::CheckPossibleGetItem</c>'s ownership window (rules checked in the order
    ///     below, first match wins): free-for-all once expired or once
    ///     <see cref="SimulationClock.GroundItemFreeForAllDelay" />
    ///     has elapsed; an item with no recorded owner is free for anyone from the moment it lands; the
    ///     recorded owner can always reclaim their own drop; monster-kill loot (<see cref="DropSort" /> ==
    ///     <see cref="MonsterKillDropSort" />) becomes claimable by the killer's own party
    ///     <see cref="SimulationClock.GroundItemPartyShareDelay" /> after creation; a manual ground-drop
    ///     (<see cref="DropSort" /> == <see cref="ManualGroundDropSort" />) is claimable by the dropper's own
    ///     party with no extra delay of its own, compared against <see cref="Master" /> rather than
    ///     <see cref="PartyName" /> (the field <c>ProcessForDropItem</c> overwrites with the dropper's party
    ///     identity for that branch only); otherwise not claimable.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame06.cpp:57-97 (<c>CheckPossibleGetItem</c>, the full rule set,
    ///     including the disabled generic-rule comment at lines 80-83) ; Server/ts25zone/S07_MyGame03.cpp:604-631
    ///     (<c>ProcessForDropItem</c>'s tail -- basis for the rule-5-vs-rule-6 field distinction) ;
    ///     Server/Header/safestring.h:26-36 (exact, case-sensitive <c>strcmp</c> equality, no case-folding).
    ///     <para>
    ///         <paramref name="claimantPartyName" /> must be the claimant's OWN currently-resolved party
    ///         identity (see <see cref="PartyIdentityResolver" />'s <c>ResolveCurrentPartyName</c>),
    ///         read live at claim time from the same <see cref="PartyRegistry" /> state the rest
    ///         of the party subsystem uses -- never a snapshot taken earlier or a hardcoded absent value. A
    ///         resolution failure for that identity should already have collapsed to an empty string upstream
    ///         (the only value the legacy field could ever hold for someone genuinely unpartied); an empty
    ///         value here can never satisfy rule 5 or rule 6, both of which require it non-empty first.
    ///     </para>
    ///     <para>
    ///         Rule 5 (the <see cref="MonsterKillDropSort" /> branch) is never satisfiable byte-for-byte in the
    ///         legacy game itself: a full-repository search found legacy's <c>iPartyName</c> is declared and
    ///         read (this exact comparison, S07_MyGame06.cpp:87) but never assigned anywhere, including the
    ///         monster-loot drop-creation function -- so equality against an always-empty legacy value could
    ///         only succeed if the claimant's own identity were also empty, which the non-empty guard already
    ///         forbids. Fenrir's own <c>MonsterSpawnScheduler.ResolvePartyDrop</c> already stamps a non-empty
    ///         <see cref="PartyName" /> on a partied kill and this repository's own
    ///         <c>MonsterSpawnSchedulerPartyLootTests</c> already exercises rule 5 as intentional, live
    ///         behavior -- so this is a documented, deliberate Fenrir divergence from strict legacy parity,
    ///         not an oversight introduced here.
    ///     </para>
    /// </remarks>
    public bool IsClaimableBy(string claimantName, string? claimantPartyName, TimeSpan nowZoneClock)
    {
        // Rule 2: free-for-all once expired or once GroundItemFreeForAllDelay has elapsed -- an
        // already-expired-ownership item is claimable by anyone, and rules 3-6 never need evaluating at all.
        if (IsExpired(nowZoneClock) || nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemFreeForAllDelay)
            return true;

        // Rule 3: no recorded owner -- free for anyone from the moment it lands.
        if (string.IsNullOrEmpty(Master))
            return true;

        // Rule 4: the recorded owner can always reclaim their own drop.
        if (string.Equals(Master, claimantName, StringComparison.Ordinal))
            return true;

        // Rule 5: monster-kill loot, inside the party-share window, claimed by the killer's own (resolved) party.
        if (DropSort == MonsterKillDropSort && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(PartyName, claimantPartyName, StringComparison.Ordinal) &&
            nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemPartyShareDelay)
            return true;

        // Rule 6: a manual ground-drop, claimed by the dropper's own (resolved) party -- compared against
        // Master (not PartyName), no extra delay of its own beyond rules 2-4 above.
        if (DropSort == ManualGroundDropSort && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(Master, claimantPartyName, StringComparison.Ordinal))
            return true;

        // Rule 7: otherwise not claimable.
        return false;
    }
}
