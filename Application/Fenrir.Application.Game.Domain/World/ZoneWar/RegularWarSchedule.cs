using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>The shared, cross-cycle authoritative phase (legacy: <c>mWorldInfo</c>'s per-war-slot value, 0-5).</summary>
public enum RegularWarPhase : byte
{
    /// <summary>Scheduling/cooldown -- sequenced internally by a local sub-phase (see <see cref="RegularWarSchedule" />).</summary>
    Idle = 0,

    /// <summary>Gate open, waiting for the final pre-war interval.</summary>
    OpenGate = 1,

    /// <summary>Final pre-war setup window (tally reset, countdown arm, server-160 smallest-tribe flag).</summary>
    PreWar = 2,

    /// <summary>The capture/score window itself.</summary>
    Active = 3,

    /// <summary>Reward payout has happened; waiting out the post-war cleanup interval.</summary>
    PostWarCleanup = 4,

    /// <summary>Heartbeat, then disconnect every session on the map, then back to <see cref="Idle" />.</summary>
    ForcedReset = 5
}

/// <summary>How the active-war window ended (or aborted).</summary>
public enum RegularWarOutcome : byte
{
    /// <summary>No reward payout beyond the always-on participation/quest/hero-point-at-losing-rate grants.</summary>
    Draw,

    /// <summary><see cref="RegularWarTickResult.WinningTribe" /> is set; full reward payout applies.</summary>
    TribeWin,

    /// <summary>
    ///     The map instance was found completely empty during the active-war window -- distinct from
    ///     <see cref="Draw" />: no reward payout of any kind happens, not even the participation counter (there
    ///     was nobody to grant it to).
    /// </summary>
    AbortedEmptyMap
}

/// <summary>
///     Per-tick ambient inputs <see cref="RegularWarSchedule.Tick" /> reads. Everything here is a plain
///     read-only snapshot the caller gathers fresh each tick -- <see cref="RegularWarSchedule" /> never reaches
///     back into a <c>Zone</c>/<c>PlayerRuntimeState</c> itself.
/// </summary>
/// <param name="TotalPresentCount">
///     Every avatar currently in a ready/connected, non-mid-transfer state on the map instance, tribed or not.
///     Used for the every-tick empty-map abort check (<see cref="RegularWarOutcome.AbortedEmptyMap" />).
/// </param>
/// <param name="PresentCountByTribe">
///     Length-4 (tribe 0-3) count of the same population, split by tribe -- untribed avatars are excluded from
///     every entry but still counted in <paramref name="TotalPresentCount" />. Used for the server-160
///     smallest-tribe flag and the mid-war elimination victory check.
/// </param>
/// <param name="BossMonsterAlive">
///     Only read on the one configured "boss war" map (295) during <see cref="RegularWarPhase.PostWarCleanup" />
///     -- whether the post-war boss spawn is still alive. Ignored everywhere else.
/// </param>
public readonly record struct RegularWarEnvironmentSnapshot(
    int TotalPresentCount,
    ImmutableArray<int> PresentCountByTribe,
    bool BossMonsterAlive = false)
{
    public static RegularWarEnvironmentSnapshot Empty { get; } = new(0, ImmutableArray.Create(0, 0, 0, 0));
}

/// <summary>Everything a caller needs to react to after one <see cref="RegularWarSchedule.Tick" /> call.</summary>
public readonly record struct RegularWarTickResult(
    RegularWarPhase Phase,
    RegularWarPhase PreviousPhase,
    int? CountdownAnnounceValue = null,
    byte? SmallestPresentTribe = null,
    RegularWarOutcome? Outcome = null,
    byte? WinningTribe = null,
    bool MonstersShouldDespawn = false,
    bool BossMonstersShouldSpawn = false,
    bool AllSessionsShouldDisconnect = false)
{
    /// <summary>True on the exact tick the active-war capture/score window begins.</summary>
    public bool EnteredActiveWar => PreviousPhase == RegularWarPhase.PreWar && Phase == RegularWarPhase.Active;
}

/// <summary>
///     One map instance's Regular War (Zone049) schedule: idle/cooldown -&gt; open-gate -&gt; pre-war -&gt;
///     active capture/score -&gt; post-war cleanup -&gt; forced reset -&gt; back to idle. Pure and I/O-free --
///     driven entirely by <see cref="Tick" /> (one legacy tick, ~500 ms, per call) and <see cref="RegisterKill" />;
///     never touches a <c>Zone</c>/<c>PlayerRuntimeState</c>/socket itself, so it is fully unit-testable without
///     any of that machinery. A caller (a Hosting-level scheduler) supplies fresh
///     <see cref="RegularWarEnvironmentSnapshot" /> inputs each tick and applies the returned
///     <see cref="RegularWarTickResult" /> against the real world.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:4920-5521 (<c>MyGame::Process_Zone_049_TYPE</c>, the full
///         cited function) ; :639-698 (<c>MyGame::Init</c> cooldown pre-seed and per-server config) ;
///         Server/Header/function.h:1642-1646 (tick-to-real-time basis, <c>GetGameTickMinute</c>).
///     </para>
///     <para>
///         <b>Cross-cycle state ownership:</b> the legacy externalizes the shared 0-5 phase value to a separate
///         <c>ts25center</c> process (<c>Server/Header/H15_MyShare.h:5</c>,
///         <c>Server/ts25center/S04_MyWork02.cpp:212-253</c>) because one legacy zone-server process hosted
///         exactly one map and needed a third party to fan its status out to the others. Fenrir has no separate
///         center process and shards by disjoint map ownership (a map's Regular War schedule is only ever
///         relevant to the one shard hosting it), so this class collapses "zone-local sub-state" and "shared
///         cross-cycle state" into one single-owner instance -- the same collapse
///         <see cref="Fenrir.Application.Game.Domain.World.WorldState.WorldStateService" /> and
///         <see cref="TribeVoteElection" /> already make for their own externally-owned legacy state. There is
///         therefore no separate relay hop to model for the status-message sort codes the legacy sent to
///         Center (1-9): each one either drives this instance's own phase transition directly (modeled below)
///         or was Discord/diagnostic-only relay with no further modeled consequence (sort 1's countdown
///         announce, and the redundant second sort-3 write -- see the Edge cases section of this cluster's
///         behavior contract).
///     </para>
///     <para>
///         <b>Not modeled by this class</b> (left to the caller / a future integration pass): the actual
///         client-facing status broadcast for this map's own wire family (already ported --
///         <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.ZoneWarTickService" />'s Zone049 case),
///         reward VALUE computation (<see cref="RegularWarRewardCalculator" />, a separate pure class), and
///         every actual mutation (money/xp/CP grants, disconnects, monster spawn/despawn, item drops) -- this
///         class only ever reports that they are due, via <see cref="RegularWarTickResult" />.
///     </para>
/// </remarks>
public sealed class RegularWarSchedule(RegularWarMapConfig config)
{
    /// <summary>Same 4-tribe width as <see cref="WorldStateService.TribeCount" /> everywhere else in this cluster.</summary>
    public const int TribeCount = WorldStateService.TribeCount;

    /// <summary>~30 min (Server/ts25zone/S07_MyGame01.cpp:4985-5017). Always paid, including the very first cycle.</summary>
    public const int CooldownTicks = 3600;

    /// <summary>~1 min cadence, counting 10 down to 1 (10 iterations total).</summary>
    public const int CountdownAnnounceIntervalTicks = 120;

    public const int CountdownAnnounceStartValue = 10;

    /// <summary>~1 min, the last Idle sub-phase before the shared phase leaves Idle.</summary>
    public const int FinalWaitTicks = 120;

    /// <summary>~3 min (Server/ts25zone/S07_MyGame01.cpp:5049-5060).</summary>
    public const int OpenGateTicks = 360;

    /// <summary>~1 min (Server/ts25zone/S07_MyGame01.cpp:5062-5132).</summary>
    public const int PreWarTicks = 120;

    /// <summary>~15 min (Server/ts25zone/S07_MyGame01.cpp:5122-5132).</summary>
    public const int ActiveWarDurationTicks = 1800;

    /// <summary>~5 s -- the cadence both the tribe-tally broadcast and victory determination share.</summary>
    public const int ActiveEvaluationCadenceTicks = 10;

    /// <summary>~1.5 min base wait, every configured map (Server/ts25zone/S07_MyGame01.cpp:5433-5502).</summary>
    public const int PostWarCleanupTicks = 180;

    /// <summary>~15 min max poll, boss-war map (295) only.</summary>
    public const int BossPollMaxTicks = 1800;

    /// <summary>~1.5 min of confirmed boss absence required after the poll ends, boss-war map only.</summary>
    public const int BossConfirmedAbsenceTicks = 180;

    /// <summary>
    ///     ~6 s after entering <see cref="RegularWarPhase.ForcedReset" />. Always reached before
    ///     <see cref="ForcedResetHeartbeatTicks" /> (120) could ever fire once -- the heartbeat is real per the
    ///     legacy source but, at these two constants' actual values, never observably fires; kept here for
    ///     citation fidelity, not because it does anything before the disconnect.
    /// </summary>
    public const int ForcedResetDisconnectAtTicks = 12;

    public const int ForcedResetHeartbeatTicks = 120;
    private readonly Dictionary<int, int> _killCountByCharacter = new();
    private readonly List<int> _killOrderSeen = [];

    private readonly int[] _tribeKillTally = new int[TribeCount];
    private int _activeEvaluationTicksElapsed;
    private int _bossAbsenceTicksElapsed;
    private bool _bossConfirmedAbsenceInProgress;
    private int _bossPollTicksElapsed;
    private int _cooldownTicksElapsed;
    private int _countdownAnnounceTicksElapsed;
    private int _countdownAnnounceValue;
    private int _finalWaitTicksElapsed;
    private int _forcedResetTicksElapsed;

    private RegularWarIdleSubPhase _idleSubPhase = RegularWarIdleSubPhase.Cooldown;
    private int _openGateTicksElapsed;
    private int _postWarTicksElapsed;
    private int _preWarTicksElapsed;

    public RegularWarPhase Phase { get; private set; } = RegularWarPhase.Idle;

    /// <summary>Set only while <see cref="RegularWarPhase.Active" /> is running down; 0 otherwise.</summary>
    public int RemainingActiveWarTicks { get; private set; }

    /// <summary>
    ///     Incremented once per cooldown-elapse (Idle sub-phase 0 -&gt; 1). Starts at 1 -- mirrors the legacy's
    ///     own pre-seed (Server/ts25zone/S07_MyGame01.cpp:639-645); purely informational, nothing branches on
    ///     its absolute value in this class.
    /// </summary>
    public int WarCycleNumber { get; private set; } = 1;

    /// <summary>Non-null only immediately after a <see cref="RegularWarOutcome.TribeWin" /> is determined.</summary>
    public byte? WinningTribe { get; private set; }

    /// <summary>
    ///     Advances by exactly one legacy tick (~500 ms). The caller is expected to bridge real elapsed time
    ///     into whole-tick calls itself (e.g. via
    ///     <see cref="Fenrir.Application.Game.Domain.Simulation.SimulationTickAccumulator" />,
    ///     same convention as <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.ZoneWarTickService" />).
    /// </summary>
    public RegularWarTickResult Tick(RegularWarEnvironmentSnapshot snapshot)
    {
        var previousPhase = Phase;
        int? countdownAnnounceValue = null;
        byte? smallestPresentTribe = null;
        RegularWarOutcome? outcome = null;
        var monstersShouldDespawn = false;
        var bossMonstersShouldSpawn = false;
        var allSessionsShouldDisconnect = false;

        switch (Phase)
        {
            case RegularWarPhase.Idle:
                TickIdle(ref countdownAnnounceValue);
                break;

            case RegularWarPhase.OpenGate:
                TickOpenGate();
                break;

            case RegularWarPhase.PreWar:
                TickPreWar(snapshot, ref smallestPresentTribe);
                break;

            case RegularWarPhase.Active:
                TickActive(snapshot, ref outcome, ref monstersShouldDespawn);
                bossMonstersShouldSpawn = outcome == RegularWarOutcome.TribeWin && config.IsBossWar;
                break;

            case RegularWarPhase.PostWarCleanup:
                TickPostWarCleanup(snapshot, ref monstersShouldDespawn);
                break;

            case RegularWarPhase.ForcedReset:
                TickForcedReset(ref allSessionsShouldDisconnect);
                break;
        }

        return new RegularWarTickResult(
            Phase,
            previousPhase,
            countdownAnnounceValue,
            smallestPresentTribe,
            outcome,
            outcome == RegularWarOutcome.TribeWin ? WinningTribe : null,
            monstersShouldDespawn,
            bossMonstersShouldSpawn,
            allSessionsShouldDisconnect);
    }

    /// <summary>
    ///     Credits one qualifying cross-tribe/RvR kill (never a duel kill -- the caller must already have
    ///     excluded those, see this cluster's behavior contract §Score accrual rule). No-ops outside
    ///     <see cref="RegularWarPhase.Active" />. The per-tribe tally additionally requires
    ///     <see cref="RemainingActiveWarTicks" /> to still be positive; the individual top-kill leaderboard does
    ///     not -- these are the two distinct gates the source contract calls out
    ///     (Server/ts25zone/S07_MyGame02.cpp:514-521 vs. :362-379).
    /// </summary>
    public void RegisterKill(byte killerTribe, int killerCharacterId)
    {
        if (Phase != RegularWarPhase.Active)
            return;

        if (!_killCountByCharacter.ContainsKey(killerCharacterId))
            _killOrderSeen.Add(killerCharacterId);

        _killCountByCharacter[killerCharacterId] = _killCountByCharacter.GetValueOrDefault(killerCharacterId) + 1;

        if (RemainingActiveWarTicks > 0 && killerTribe < TribeCount)
            _tribeKillTally[killerTribe]++;
    }

    public int GetTribeKillTally(byte tribeId)
    {
        return tribeId < TribeCount ? _tribeKillTally[tribeId] : 0;
    }

    /// <summary>
    ///     Up to <paramref name="count" /> character ids ordered by cross-tribe kills accumulated this cycle,
    ///     highest first; ties broken by whichever character reached that count first (the legacy source cited
    ///     for <c>RW_SetTopKill</c>/<c>RW_Reward</c> doesn't spell out its own tie-break, so this is a documented,
    ///     deterministic choice, not a verified one). Characters with zero kills are never included.
    /// </summary>
    public ImmutableArray<int> GetTopKillers(int count = 3)
    {
        if (_killOrderSeen.Count == 0)
            return [];

        return
        [
            .. _killOrderSeen
                .Where(id => _killCountByCharacter[id] > 0)
                .OrderByDescending(id => _killCountByCharacter[id])
                .Take(count)
        ];
    }

    private void TickIdle(ref int? countdownAnnounceValue)
    {
        switch (_idleSubPhase)
        {
            case RegularWarIdleSubPhase.Cooldown:
                _cooldownTicksElapsed++;
                if (_cooldownTicksElapsed < CooldownTicks)
                    return;

                _killCountByCharacter.Clear();
                _killOrderSeen.Clear();
                WarCycleNumber++;
                _countdownAnnounceValue = CountdownAnnounceStartValue;
                _countdownAnnounceTicksElapsed = 0;
                _idleSubPhase = RegularWarIdleSubPhase.CountdownAnnounce;
                return;

            case RegularWarIdleSubPhase.CountdownAnnounce:
                _countdownAnnounceTicksElapsed++;
                if (_countdownAnnounceTicksElapsed < CountdownAnnounceIntervalTicks)
                    return;

                _countdownAnnounceTicksElapsed = 0;
                countdownAnnounceValue = _countdownAnnounceValue;
                _countdownAnnounceValue--;

                if (_countdownAnnounceValue <= 0)
                {
                    _idleSubPhase = RegularWarIdleSubPhase.FinalWait;
                    _finalWaitTicksElapsed = 0;
                }

                return;

            case RegularWarIdleSubPhase.FinalWait:
                _finalWaitTicksElapsed++;
                if (_finalWaitTicksElapsed < FinalWaitTicks)
                    return;

                Phase = RegularWarPhase.OpenGate;
                _openGateTicksElapsed = 0;
                return;
        }
    }

    private void TickOpenGate()
    {
        _openGateTicksElapsed++;
        if (_openGateTicksElapsed < OpenGateTicks)
            return;

        Phase = RegularWarPhase.PreWar;
        _preWarTicksElapsed = 0;
    }

    private void TickPreWar(RegularWarEnvironmentSnapshot snapshot, ref byte? smallestPresentTribe)
    {
        _preWarTicksElapsed++;
        if (_preWarTicksElapsed < PreWarTicks)
            return;

        if (config.AnnouncesSmallestPresentTribe)
            smallestPresentTribe = ComputeSmallestPresentTribe(snapshot.PresentCountByTribe);

        Array.Clear(_tribeKillTally);
        RemainingActiveWarTicks = ActiveWarDurationTicks;
        _activeEvaluationTicksElapsed = 0;
        Phase = RegularWarPhase.Active;
    }

    private void TickActive(RegularWarEnvironmentSnapshot snapshot, ref RegularWarOutcome? outcome,
        ref bool monstersShouldDespawn)
    {
        if (snapshot.TotalPresentCount <= 0)
        {
            WinningTribe = null;
            outcome = RegularWarOutcome.AbortedEmptyMap;
            monstersShouldDespawn = true;
            EnterForcedReset();
            return;
        }

        if (RemainingActiveWarTicks > 0)
            RemainingActiveWarTicks--;

        _activeEvaluationTicksElapsed++;
        if (_activeEvaluationTicksElapsed < ActiveEvaluationCadenceTicks)
            return;

        _activeEvaluationTicksElapsed = 0;

        var determined = RemainingActiveWarTicks <= 0
            ? DetermineTimeoutOutcome()
            : DetermineEliminationOutcome(snapshot.PresentCountByTribe);

        if (determined is null)
            return; // war continues -- two or more tribes still present, countdown still running

        outcome = determined;
        monstersShouldDespawn = true;
        Phase = RegularWarPhase.PostWarCleanup;
        _postWarTicksElapsed = 0;
        _bossPollTicksElapsed = 0;
        _bossConfirmedAbsenceInProgress = false;
        _bossAbsenceTicksElapsed = 0;
    }

    private void TickPostWarCleanup(RegularWarEnvironmentSnapshot snapshot, ref bool monstersShouldDespawn)
    {
        _postWarTicksElapsed++;
        if (_postWarTicksElapsed < PostWarCleanupTicks)
            return;

        if (!config.IsBossWar)
        {
            monstersShouldDespawn = true;
            EnterForcedReset();
            return;
        }

        if (!_bossConfirmedAbsenceInProgress)
        {
            if (_bossPollTicksElapsed < BossPollMaxTicks && snapshot.BossMonsterAlive &&
                snapshot.TotalPresentCount > 0)
            {
                _bossPollTicksElapsed++;
                return;
            }

            _bossConfirmedAbsenceInProgress = true;
            _bossAbsenceTicksElapsed = 0;
            return;
        }

        _bossAbsenceTicksElapsed++;
        if (_bossAbsenceTicksElapsed < BossConfirmedAbsenceTicks)
            return;

        monstersShouldDespawn = true;
        EnterForcedReset();
    }

    private void TickForcedReset(ref bool allSessionsShouldDisconnect)
    {
        _forcedResetTicksElapsed++;
        if (_forcedResetTicksElapsed < ForcedResetDisconnectAtTicks)
            return;

        allSessionsShouldDisconnect = true;

        Phase = RegularWarPhase.Idle;
        _idleSubPhase = RegularWarIdleSubPhase.Cooldown;
        _cooldownTicksElapsed = 0;

        // A war that concluded before the countdown ran out (an elimination win, a no-tribes-present draw, or
        // an aborted empty map) leaves a non-zero leftover here -- deliberately still readable as "how much
        // time was left when the war ended" throughout PostWarCleanup/ForcedReset (see
        // Active_ElimintationVictory_CanHappenLongBeforeTheCountdownExpires). Only reset once the cycle is
        // fully back to Idle, matching RemainingActiveWarTicks's own contract ("0 otherwise").
        RemainingActiveWarTicks = 0;
    }

    private void EnterForcedReset()
    {
        Phase = RegularWarPhase.ForcedReset;
        _forcedResetTicksElapsed = 0;
    }

    /// <summary>
    ///     Strictly-highest-and-positive-else-draw tally comparison, evaluated only at countdown expiry.
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5209-5244.
    /// </summary>
    private RegularWarOutcome DetermineTimeoutOutcome()
    {
        var max = 0;
        for (byte t = 0; t < TribeCount; t++)
            if (_tribeKillTally[t] > max)
                max = _tribeKillTally[t];

        if (max <= 0)
        {
            WinningTribe = null;
            return RegularWarOutcome.Draw;
        }

        byte? winner = null;
        for (byte t = 0; t < TribeCount; t++)
            if (_tribeKillTally[t] == max)
            {
                if (winner is not null)
                {
                    // A second tribe shares the max -- tie, draw.
                    WinningTribe = null;
                    return RegularWarOutcome.Draw;
                }

                winner = t;
            }

        WinningTribe = winner;
        return RegularWarOutcome.TribeWin;
    }

    /// <summary>
    ///     Physical-presence elimination check, evaluated only while the countdown is still running -- entirely
    ///     independent of <see cref="_tribeKillTally" />. Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5245-5284.
    /// </summary>
    private RegularWarOutcome? DetermineEliminationOutcome(ImmutableArray<int> presentCountByTribe)
    {
        var presentTribes = 0;
        byte soleTribe = 0;

        for (byte t = 0; t < TribeCount; t++)
            if (t < presentCountByTribe.Length && presentCountByTribe[t] > 0)
            {
                presentTribes++;
                soleTribe = t;
            }

        switch (presentTribes)
        {
            case 0:
                WinningTribe = null;
                return RegularWarOutcome.Draw;
            case 1:
                WinningTribe = soleTribe;
                return RegularWarOutcome.TribeWin;
            default:
                return null; // two or more tribes still present -- continue
        }
    }

    /// <summary>
    ///     Lowest present-count wins; ties broken by lowest tribe id. Réf. C++ : Server/Header/function.h:3167-3179
    ///     -- the exact tie-break of that helper wasn't independently re-verified this session, so this is a
    ///     documented, deterministic assumption rather than a verified transcription.
    /// </summary>
    private static byte ComputeSmallestPresentTribe(ImmutableArray<int> presentCountByTribe)
    {
        byte smallestTribe = 0;
        var smallestCount = int.MaxValue;

        for (byte t = 0; t < TribeCount; t++)
        {
            var count = t < presentCountByTribe.Length ? presentCountByTribe[t] : 0;
            if (count < smallestCount)
            {
                smallestCount = count;
                smallestTribe = t;
            }
        }

        return smallestTribe;
    }

    private enum RegularWarIdleSubPhase : byte
    {
        Cooldown = 0,
        CountdownAnnounce = 1,
        FinalWait = 2
    }
}
