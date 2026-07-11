using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Dedicated <see cref="MonsterEntity.ServerIndex" /> pool for the Valley of the Deceased (Zone200/297/298/
    ///     299 gate-breach/kill-race) Boss-756 summon -- same "each ad-hoc/reserved spawn family gets its own
    ///     non-overlapping base" convention already established by
    ///     <c>ZoneWar.TribeGuardSpawner.OrdinaryPoolServerIndexBase</c>/<c>Zone038WinnerPoolServerIndexBase</c>
    ///     (1_000_000/1_001_000), <c>ZoneWar.TribeSymbolSpawner.SymbolPoolServerIndexBase</c> (1_002_000),
    ///     <see cref="GmSummonPoolServerIndexBase" /> (1_003_000, see <c>Zone.Monsters.cs</c>), and
    ///     <see cref="RegularWarBossPoolServerIndexBase" /> (1_004_000, see <c>Zone.RegularWarBossSummon.cs</c>) --
    ///     this is the next one, well clear of every sibling pool and of <see cref="Monsters.MonsterSpawnScheduler" />'s
    ///     own ordinary per-zone slot numbering.
    /// </summary>
    private const int ValleyWarBossPoolServerIndexBase = 1_005_000;

    /// <summary>
    ///     Size of <see cref="ValleyWarBossPoolServerIndexBase" />'s reserved range. Unlike
    ///     <see cref="RegularWarBossPoolSize" />, this family's own summon call configures its existence check ON
    ///     (<see cref="Zone200GateBreachBossCatalog.ExistenceCheckActive" />) and this handler re-checks THIS pool
    ///     for a live copy before every summon attempt, so at most one live Boss-756 copy can ever occupy this
    ///     range regardless of whether the legacy map-wide "clear every summoned monster" step (also unmodeled
    ///     here -- same open gap as Boss 561's, see <see cref="HandleSummonRegularWarBoss" />'s own remarks) ever
    ///     runs. A handful of slots is a defensive cushion, not evidence more than one copy is ever expected.
    /// </summary>
    private const int ValleyWarBossPoolSize = 10;

    /// <summary>
    ///     No legacy leash-radius constant applies to this stationary kill-race-conclusion boss (the source
    ///     contract's own citations describe only its summon position, never a patrol/leash behavior) -- same
    ///     generous engineering stand-in posture as <see cref="RegularWarBossLeashRadius" />/
    ///     <see cref="GmSummonLeashRadius" />, not a legacy-parity claim.
    /// </summary>
    private const float ValleyWarBossLeashRadius = 200f;

    /// <summary>
    ///     <see cref="ValleyWarSystem" />'s Boss-756 summon primitive -- the ONLY site permitted to summon
    ///     it for a Valley of the Deceased (Zone200/297/298/299) kill-race win, preserving the
    ///     single-writer-per-zone invariant. Unlike <see cref="HandleSummonRegularWarBoss" />, no
    ///     <see cref="ZoneCommand" />/<see cref="Post" /> hop is needed here: <see cref="ValleyWarSystem" />
    ///     is a genuine <see cref="ISimulationSystem" /> that already runs directly on this zone's own tick thread
    ///     inside <see cref="Simulate" />, so it can call this method inline -- see that class's own class-level
    ///     remarks for why it, unlike <c>RegularWarRewardGrantSink</c>, needs no cross-thread <see cref="Post" />
    ///     hop for this particular side effect.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11413-11416 (fixed summon coordinate + the single summon
    ///         call) ; Server/ts25zone/S10_MySummon.cpp:1254 (shared summon routine, existence-check-ON path --
    ///         <see cref="Zone200GateBreachBossCatalog.ExistenceCheckActive" />, confirmed the only summon call for
    ///         monster 756 anywhere in that source file). See <see cref="Zone200GateBreachBossCatalog" />'s own
    ///         remarks for the concrete monster id / fixed position this handler consumes.
    ///     </para>
    ///     <para>
    ///         Exactly ONE summon call, unlike <see cref="HandleSummonRegularWarBoss" />'s three -- this catalog
    ///         has no analogous <c>SummonCount</c> constant because there is only ever one. Before placing it, this
    ///         handler scans its own reserved <see cref="ValleyWarBossPoolServerIndexBase" /> range for a monster
    ///         whose <see cref="MonsterEntity.Template" />'s
    ///         <see cref="Fenrir.Data.Abstractions.World.MonsterRowDto.MonsterId" /> already equals
    ///         <see cref="Zone200GateBreachBossCatalog.BossMonsterId" /> and no-ops if one is found -- the one real
    ///         behavioral difference from Boss 561's summon, which skips this check entirely
    ///         (<see cref="RegularWarBossSummonCatalog.SkipExistenceCheck" />).
    ///     </para>
    ///     <para>
    ///         <b>Not modeled here, deliberately:</b> the surrounding gate-breach/kill-race state machine itself
    ///         (already modeled by <see cref="ValleyWarSchedule" />/<see cref="ValleyWarSystem" />)
    ///         and everything else that class's own remarks still flag as a genuine gap (the general kill-race
    ///         monster population, the "animal5" reward item) -- this handler closes ONLY the boss-756 summon
    ///         primitive itself.
    ///     </para>
    /// </remarks>
    internal void HandleSummonValleyWarBoss()
    {
        if (!worldData.MonstersById.TryGetValue(Zone200GateBreachBossCatalog.BossMonsterId, out var definition))
            return;

        if (Zone200GateBreachBossCatalog.ExistenceCheckActive && ValleyWarBossAlreadyLive())
            return;

        if (!TryFindFreeValleyWarBossSlot(out var serverIndex))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            Zone200GateBreachBossCatalog.SummonX, Zone200GateBreachBossCatalog.SummonY,
            Zone200GateBreachBossCatalog.SummonZ, ValleyWarBossLeashRadius);

        SpawnMonster(monster);
    }

    private bool ValleyWarBossAlreadyLive()
    {
        for (var i = 0; i < ValleyWarBossPoolSize; i++)
        {
            var candidate = ValleyWarBossPoolServerIndexBase + i;
            if (_monsters.TryGetValue(candidate, out var monster) &&
                monster.Template.MonsterId == Zone200GateBreachBossCatalog.BossMonsterId)
                return true;
        }

        return false;
    }

    private bool TryFindFreeValleyWarBossSlot(out int serverIndex)
    {
        for (var i = 0; i < ValleyWarBossPoolSize; i++)
        {
            var candidate = ValleyWarBossPoolServerIndexBase + i;
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
