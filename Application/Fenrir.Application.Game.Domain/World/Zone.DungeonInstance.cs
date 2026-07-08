using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>Result of <see cref="Zone.TryEnterZone241PersonalInstance" />.</summary>
public enum DungeonInstanceEntryOutcome
{
    /// <summary>This zone is not configured as Zone-241-type (<see cref="Zone.IsZone241TypeZone" /> false).</summary>
    NotZone241Type,

    /// <summary><see cref="PlayerRuntimeState.DungeonInstanceRoundsRemaining" /> was below 1 -- refused, no state touched.</summary>
    QuotaExhausted,

    /// <summary>
    ///     No boss could be resolved/summoned for this avatar's rebirth tier -- the just-armed instance was torn
    ///     down (the one live cleanup call site) and entry refused.
    /// </summary>
    SummonFailed,

    /// <summary>Armed and the personal boss is live; one unit of the quota was consumed.</summary>
    Entered
}

/// <summary>
///     Zone-241 "LOD" personal-boss-chain dungeon: per-avatar content partitioning (NOT per-party -- see the
///     class-level framing correction this was implemented from). One <see cref="PlayerRuntimeState" /> per
///     avatar carries its own <see cref="PlayerRuntimeState.DungeonInstanceId" />/
///     <see cref="PlayerRuntimeState.DungeonInstanceLifecycleState" />/
///     <see cref="PlayerRuntimeState.DungeonInstanceTick" />,
///     mirroring legacy's one <c>DUNGEON_INSTANCE</c> struct embedded per connected avatar
///     (<c>USER_OBJECT::mInstance</c>) -- never a registry keyed by party.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S03_MyUser.cpp:26-136 (<c>DUNGEON_INSTANCE</c> Init/Free/ClearMonster/ClearItem,
///     the <c>mState == 2</c> cleanup guard, the always-empty <c>INSTANCE_STL</c>-gated AddMonster/AddItem) ;
///     Server/ts25zone/H07_MyGame.h:24-41,889 (struct shape, one-per-avatar) ;
///     Server/ts25zone/S04_MyWork02.cpp:1142-1194 (zone-enter re-arm + summon) ;
///     Server/ts25zone/S07_MyGame03.cpp:5946-5960,6240-6330 (the 325-330 server-number gate, quota check, forced
///     monster-slot reuse, the one live cleanup call site) ; Server/ts25zone/S07_MyGame01.cpp:11763-12013
///     (the tick-driven state machine, states 0-8).
///     <para>
///         <b>Trigger 1</b> (connection-slot alloc/free resets the instance struct) needs no code here: every
///         <see cref="PlayerRuntimeState" /> is a fresh object built by <see cref="Zone.HandleEnter" /> and
///         discarded by <see cref="Zone.HandleLeave" />, so the instance fields already start at their declared
///         defaults (<see cref="DungeonInstanceLifecycle.Idle" />, null id, tick 0) on every entry -- including
///         every in-process zone-transfer hop, matching legacy's own per-zone-connection <c>MyUser::Init</c>/
///         <c>Free</c> granularity (each legacy zone-to-zone hop is a real socket reconnect to a fresh
///         <c>MyUser</c> slot, unlike Fenrir's same-TCP-connection handoff -- but the RESET granularity, once
///         per zone-entry, is identical either way).
///     </para>
///     <para>
///         <b>Deliberately not implemented</b> (the contract this was built from does not specify these, and
///         guessing would violate the "no legacy parity from memory" rule):
///         <see cref="DungeonInstanceLifecycle.Waiting" />
///         is never entered (the one arm call site jumps straight to <see cref="DungeonInstanceLifecycle.Summoning" />);
///         <see cref="DungeonInstanceLifecycle.Failure" />'s trigger condition (only
///         <see cref="DungeonInstanceLifecycle.Success" />,
///         on the personal boss's death, is driven); the <see cref="DungeonInstanceLifecycle.WaitBeforeReturn" />/
///         <see cref="DungeonInstanceLifecycle.ReturnToTownNotice" />/
///         <see cref="DungeonInstanceLifecycle.DisconnectPending" />
///         tick durations and the auto-return-to-town teleport/forced-disconnect side effects those states
///         imply. The two divergent tier-&gt;boss lookup tables (differing at tiers 4/5 between the entry-flow
///         and tick-summon copies) are represented only by <see cref="IPersonalDungeonBossCatalog" />'s single
///         method -- see that interface's own remarks. A follow-up legacy-behavior-translator contract citing
///         concrete tick counts/monster ids for these is needed before they can be implemented for real.
///     </para>
/// </remarks>
public sealed partial class Zone
{
    /// <summary>
    ///     S07_MyGame01.cpp:11923-11927's own 20-tick status-broadcast cadence while
    ///     <see cref="DungeonInstanceLifecycle.BattleInProgress" />.
    /// </summary>
    private const int PersonalDungeonBattleBroadcastCadenceTicks = 20;

    /// <summary>
    ///     No legacy leash-radius constant applies to a personal 1:1 boss fight (it never patrols away from its
    ///     summon point in this implementation) -- a generous engineering stand-in, same posture as
    ///     <see cref="MonsterAiSystem" />'s own "no constant found" remarks, not a legacy-parity claim.
    /// </summary>
    private const float PersonalDungeonBossLeashRadius = 200f;

    /// <summary>
    ///     Resolves the tier-&gt;boss lookup for <see cref="TryEnterZone241PersonalInstance" />. Defaults to
    ///     <see cref="NullPersonalDungeonBossCatalog" /> (every entry attempt's summon step fails) until Hosting
    ///     wires up a real one -- see that type's own remarks.
    /// </summary>
    public IPersonalDungeonBossCatalog PersonalDungeonBossCatalog { get; set; } =
        NullPersonalDungeonBossCatalog.Instance;

    /// <summary>
    ///     Whether this zone is configured to run the Zone-241 "LOD" personal dungeon content -- the Fenrir
    ///     equivalent of legacy's boot-time <c>mCheckZone241TypeServer</c> arm (see
    ///     <see cref="GameServerOptions.Zone241DungeonMapIds" />'s own remarks for the server-number-to-map-id
    ///     translation).
    /// </summary>
    public bool IsZone241TypeZone => options.Zone241DungeonMapIds.Contains(MapId);

    /// <summary>
    ///     Trigger 2: the zone-enter handler's unconditional re-arm + immediate personal-boss summon attempt,
    ///     called automatically from <see cref="HandleEnter" /> whenever <see cref="IsZone241TypeZone" />. Also
    ///     directly callable (e.g. by tests) to retry entry for an avatar already tracked in this zone.
    /// </summary>
    public DungeonInstanceEntryOutcome TryEnterZone241PersonalInstance(int characterId)
    {
        if (!IsZone241TypeZone)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (!_players.TryGetValue(characterId, out var state) || state is null)
            return DungeonInstanceEntryOutcome.NotZone241Type; // not tracked here -- defensive only

        // Server/ts25zone/S07_MyGame03.cpp:6272-6275: quota gate. Refused outright, no instance state touched.
        if (state.DungeonInstanceRoundsRemaining < 1)
            return DungeonInstanceEntryOutcome.QuotaExhausted;

        // Side effect #1: unconditional re-arm (S04_MyWork02.cpp:1151-1153; S07_MyGame03.cpp:6247-6249).
        state.DungeonInstanceId = state.CharacterId;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        state.DungeonInstanceTick = 0;

        if (!PersonalDungeonBossCatalog.TryGetBossMonsterId(state.RebirthCount, out var monsterId) ||
            !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
        {
            TearDownFailedEntry(state);
            return DungeonInstanceEntryOutcome.SummonFailed;
        }

        SummonPersonalBoss(state, monsterDefinition.Monster);

        state.DungeonInstanceRoundsRemaining--;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.BattleInProgress;
        return DungeonInstanceEntryOutcome.Entered;
    }

    /// <summary>
    ///     Side effect #2: forcibly reuses the global monster slot whose index equals the avatar's own
    ///     connection-slot index (here, <see cref="PlayerRuntimeState.CharacterId" />), silently discarding
    ///     whatever previously occupied it -- no despawn broadcast, no loot, no <see cref="DeadMonsterEvent" />.
    ///     This is destructive by construction (Server/ts25zone/S07_MyGame01.cpp:11884-11885;
    ///     Server/ts25zone/S07_MyGame03.cpp:6309; Server/ts25zone/S04_MyWork02.cpp:1188) and structurally risks
    ///     colliding with <see cref="MonsterSpawnScheduler" />'s own per-region slot numbering if a
    ///     <see cref="PlayerRuntimeState.CharacterId" /> happens to fall inside that scheduler's small
    ///     per-zone slot range -- a real, inherited structural risk this implementation faithfully preserves
    ///     rather than papers over (see the behavior contract's own Edge Cases for why this was not "fixed").
    /// </summary>
    private void SummonPersonalBoss(PlayerRuntimeState state, MonsterRowDto template)
    {
        var serverIndex = state.CharacterId;

        if (_monsters.TryRemove(serverIndex, out var displaced))
            RemoveMonsterFromGrid(displaced);

        var boss = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), template, serverIndex,
            state.PosX, state.PosY, state.PosZ, PersonalDungeonBossLeashRadius, serverIndex);

        SpawnMonster(boss);
    }

    /// <summary>The one live cleanup call site: the synchronous summon-failure path, guard still true (see class remarks).</summary>
    private void TearDownFailedEntry(PlayerRuntimeState state)
    {
        ClearZone241PersonalDungeonInstance(state);

        // Not independently re-derived from a specific citation for the exact post-failure mState: Idle is the
        // conservative terminus consistent with "entry refused" and the struct's own documented default.
        state.DungeonInstanceId = null;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Idle;
    }

    /// <summary>
    ///     <c>ClearMonster</c>/<c>ClearItem</c>'s shared guard (Server/ts25zone/S03_MyUser.cpp:73,104): a no-op
    ///     unless <paramref name="state" />'s own lifecycle is still exactly <see cref="DungeonInstanceLifecycle.Summoning" />
    ///     at the moment this runs -- true only at <see cref="TearDownFailedEntry" />'s call site; every other
    ///     caller (Success/Failure/DisconnectPending transitions) has already moved state past Summoning by the
    ///     time it would call this, so the scan below never actually frees anything there, faithfully
    ///     reproducing legacy's own dead-in-practice cleanup (see the behavior contract's own Edge Cases).
    ///     <see cref="IPersonalDungeonBossCatalog" />'s sibling <c>INSTANCE_STL</c> membership list is dead code
    ///     in legacy too (never defined), so this scans every live monster/item instead of a tracked list --
    ///     the faithful equivalent, not a Fenrir shortcut.
    /// </summary>
    private void ClearZone241PersonalDungeonInstance(PlayerRuntimeState state)
    {
        if (state.DungeonInstanceLifecycleState != DungeonInstanceLifecycle.Summoning)
            return;

        if (state.DungeonInstanceId is not { } instanceId)
            return;

        foreach (var (index, monster) in _monsters)
            if (monster.InstanceId == instanceId && _monsters.TryRemove(index, out _))
                RemoveMonsterFromGrid(monster);

        foreach (var (index, item) in _groundItems)
            if (item.InstanceId == instanceId)
                _groundItems.TryRemove(index, out _);
    }

    /// <summary>
    ///     Side effect #4's broadcast-visibility filter (Server/ts25zone/S07_MyGame03.cpp:826,874,926,971): an
    ///     untagged object (<paramref name="objectInstanceId" /> null -- every ordinary, non-personal-instance
    ///     monster/item) is visible to everyone, matching legacy's own filter being active only under
    ///     <c>mCheckZone241TypeServer</c> -- since only this mechanism ever tags an object with a non-null
    ///     instance id in the first place, gating on the tag itself is equivalent to gating on the zone-type
    ///     flag, without needing to re-check <see cref="IsZone241TypeZone" /> at every call site.
    /// </summary>
    private static bool IsVisibleAcrossDungeonInstance(int? objectInstanceId, int? viewerInstanceId)
    {
        return objectInstanceId is not { } required || required == viewerInstanceId;
    }

    /// <summary>
    ///     Trigger 3: <c>MyGame::Logic()</c>'s once-per-tick <c>Process_Zone_241_TYPE()</c> call
    ///     (S07_MyGame01.cpp:3020), restricted to the states/transitions this pass models -- see class remarks.
    ///     Called from <see cref="Tick" /> only while <see cref="IsZone241TypeZone" /> AND at least one legacy
    ///     (500 ms) tick elapsed this frame -- <paramref name="legacyTicksElapsed" /> is how many, so a
    ///     stalled host's multi-tick catch-up still advances <see cref="PlayerRuntimeState.DungeonInstanceTick" />
    ///     by the correct amount rather than by a flat 1 per physical 20 Hz frame (see
    ///     fenrir-tick-loop-self-critique's dungeon-instance-cadence finding: the previous ungated,
    ///     always-+1 version fired <see cref="PersonalDungeonBattleBroadcastCadenceTicks" />'s 20-tick
    ///     status broadcast ~10x too often, once per second instead of once per ~10 s).
    /// </summary>
    public void AdvanceZone241PersonalDungeonInstances(int legacyTicksElapsed)
    {
        if (!IsZone241TypeZone)
            return;

        foreach (var state in _players.Values)
        {
            if (state.DungeonInstanceLifecycleState == DungeonInstanceLifecycle.Idle)
                continue;

            state.DungeonInstanceTick += legacyTicksElapsed;

            if (state.DungeonInstanceLifecycleState != DungeonInstanceLifecycle.BattleInProgress)
                continue;

            var instanceId = state.DungeonInstanceId ?? state.CharacterId;
            var bossAlive = _monsters.TryGetValue(instanceId, out var boss) && boss is not null &&
                            boss.InstanceId == instanceId;

            if (!bossAlive)
            {
                // Success: the personal boss is dead. Failure's own trigger condition is out of scope -- see
                // class remarks.
                state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Success;
                ClearZone241PersonalDungeonInstance(state); // dead-in-practice guard, see remarks -- a no-op here too
                continue;
            }

            if (state.DungeonInstanceTick % PersonalDungeonBattleBroadcastCadenceTicks == 0)
                // S07_MyGame01.cpp:11923-11927: RemainTime's own countdown-duration source is not covered by
                // the contract this was implemented from, so this always reports 0 rather than fabricating a
                // countdown -- the cadence itself (fires every 20 ticks) is the cited, faithfully-reproduced part.
                state.Session.Send(new ZoneWar241StatusResponse { RemainTime = 0 });
        }
    }
}
