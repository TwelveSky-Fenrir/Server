using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Dedicated <see cref="MonsterEntity.ServerIndex" /> pool for the Regular War (Zone049, legacy server
    ///     295 only) Boss-561 summon -- same "each ad-hoc/reserved spawn family gets its own non-overlapping
    ///     base" convention already established by <c>ZoneWar.TribeGuardSpawner.OrdinaryPoolServerIndexBase</c>/
    ///     <c>Zone038WinnerPoolServerIndexBase</c> (1_000_000/1_001_000),
    ///     <c>ZoneWar.TribeSymbolSpawner.SymbolPoolServerIndexBase</c> (1_002_000), and
    ///     <see cref="GmSummonPoolServerIndexBase" /> (1_003_000, see <c>Zone.Monsters.cs</c>) -- this is the
    ///     next one, well clear of every sibling pool and of <see cref="Monsters.MonsterSpawnScheduler" />'s own
    ///     ordinary per-zone slot numbering.
    /// </summary>
    private const int RegularWarBossPoolServerIndexBase = 1_004_000;

    /// <summary>
    ///     Size of <see cref="RegularWarBossPoolServerIndexBase" />'s reserved range -- generous relative to
    ///     <see cref="RegularWarBossSummonCatalog.SummonCount" /> (3) since the legacy map-wide "clear every
    ///     summoned monster on this map instance first" step (Server/ts25zone/S07_MyGame01.cpp:5405-5410) is
    ///     NOT modeled here yet -- see <see cref="HandleSummonRegularWarBoss" />'s own remarks. Without it, a
    ///     Fenrir zone could in principle accumulate leftover boss-561 copies across repeated Regular War
    ///     cycles rather than only ever holding exactly 3 at a time; 100 slots is a defensive cushion against
    ///     that, not a legacy-cited figure.
    /// </summary>
    private const int RegularWarBossPoolSize = 100;

    /// <summary>
    ///     No legacy leash-radius constant applies to this stationary battle-conclusion boss (the source
    ///     contract's own citations describe only its summon position, never a patrol/leash behavior) -- same
    ///     generous engineering stand-in posture as <see cref="GmSummonLeashRadius" />/
    ///     <see cref="PersonalDungeonBossLeashRadius" />, not a legacy-parity claim.
    /// </summary>
    private const float RegularWarBossLeashRadius = 200f;

    /// <summary>
    ///     <see cref="ZoneCommandKind.SummonRegularWarBoss" />'s handler -- the ONLY site permitted to summon
    ///     Boss 561 for a Regular War (Zone049, server 295 only) conclusion, preserving the
    ///     single-writer-per-zone invariant (this runs on the zone's own tick thread; the poster,
    ///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarRewardGrantSink</c>, runs on
    ///     <c>RegularWarSchedulerHost</c>'s own background-timer thread and never touches
    ///     <see cref="_monsters" /> directly -- same cross-thread precedent as
    ///     <see cref="HandleApplyRegularWarReward" />).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5405-5432 (win-branch: map clear, win broadcast,
    ///         then the 3-call boss summon loop) ; Server/ts25zone/S10_MySummon.cpp:1254-1297 (shared summon
    ///         routine, existence-check-off path) ; Server/ts25zone/H01_MainApplication.h:75-95 (shared
    ///         100-slot special/boss sub-range within the overall 3000-slot monster pool). See
    ///         <see cref="RegularWarBossSummonCatalog" />'s own remarks for the concrete monster id / summon
    ///         count / fixed position this handler consumes.
    ///     </para>
    ///     <para>
    ///         Exactly <see cref="RegularWarBossSummonCatalog.SummonCount" /> (3) summon calls, back to back,
    ///         each placing one copy of <see cref="RegularWarBossSummonCatalog.BossMonsterId" /> (561) at the
    ///         SAME fixed position (<see cref="RegularWarBossSummonCatalog.SummonX" />/<c>SummonY</c>/
    ///         <c>SummonZ</c>). Per <see cref="RegularWarBossSummonCatalog.SkipExistenceCheck" />, no
    ///         duplicate-of-561 check runs before any of the 3 calls -- each independently succeeds as long as
    ///         a free slot exists in <see cref="RegularWarBossPoolServerIndexBase" />'s reserved range; a full
    ///         pool silently drops that one placement (no error, no retry), same failure mode as every sibling
    ///         summon primitive in this codebase (<see cref="SpawnGmSummonedMonster" />,
    ///         <see cref="SummonPersonalBoss" />).
    ///     </para>
    ///     <para>
    ///         <b>Not modeled here, deliberately:</b> the legacy map-wide "clear every monster this map
    ///         instance currently has summoned, from ANY source" step that immediately precedes the 3 summon
    ///         calls (S07_MyGame01.cpp:5405-5410). Regular War relies entirely on that clear to avoid
    ///         duplicate bosses across conclusions -- Fenrir has no existing "wipe every summoned monster on a
    ///         zone regardless of spawn source" API, and inventing one is out of scope for this narrow,
    ///         well-cited summon-primitive gap (a map-wide clear would need its own dedicated design pass,
    ///         since it would also affect ordinary <see cref="Monsters.MonsterSpawnScheduler" />-owned
    ///         monsters, tribe-guard/symbol pools, etc. -- not something to improvise here). Flagged as an
    ///         open follow-up rather than silently skipped: without it, a Regular War map that concludes
    ///         decisively more than <see cref="RegularWarBossPoolSize" />/<see cref="RegularWarBossSummonCatalog.SummonCount" />
    ///         times without its 3 prior boss copies ever dying will eventually exhaust
    ///         <see cref="RegularWarBossPoolServerIndexBase" />'s range and start silently dropping new copies.
    ///     </para>
    /// </remarks>
    private void HandleSummonRegularWarBoss()
    {
        if (!worldData.MonstersById.TryGetValue(RegularWarBossSummonCatalog.BossMonsterId, out var definition))
            return;

        for (var i = 0; i < RegularWarBossSummonCatalog.SummonCount; i++)
        {
            if (!TryFindFreeRegularWarBossSlot(out var serverIndex))
                return;

            var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster,
                serverIndex, RegularWarBossSummonCatalog.SummonX, RegularWarBossSummonCatalog.SummonY,
                RegularWarBossSummonCatalog.SummonZ, RegularWarBossLeashRadius);

            SpawnMonster(monster);
        }
    }

    private bool TryFindFreeRegularWarBossSlot(out int serverIndex)
    {
        for (var i = 0; i < RegularWarBossPoolSize; i++)
        {
            var candidate = RegularWarBossPoolServerIndexBase + i;
            if (!_monsters.ContainsKey(candidate))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }
}
