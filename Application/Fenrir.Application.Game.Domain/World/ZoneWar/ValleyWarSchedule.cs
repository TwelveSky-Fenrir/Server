using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The shared world-state value for one Valley of the Deceased (Zone 200/297/298/299) "Regular War" map
///     instance -- a DISTINCT legacy state machine from the Zone049 family already modeled by
///     <see cref="RegularWarSchedule" /> (different function, different maps, different tSort numbers; see this
///     class's own remarks). Numeric values match the legacy shared world-state values 0-8 verbatim, for easy
///     cross-reference against the source contract.
/// </summary>
public enum ValleyWarPhase : byte
{
    /// <summary>6-hour idle wait since the last full cycle reset.</summary>
    Idle = 0,

    /// <summary>Counting down the gate-open value (5,4,3,2,1), one send per elapsed real minute.</summary>
    GateCountdown = 1,

    /// <summary>Gate open, closing in one more real minute.</summary>
    GateOpen = 2,

    /// <summary>Gate closed; a ~10 real-second, zone-local-only countdown before the door opens.</summary>
    DoorPending = 3,

    /// <summary>The kill-race window: per-tribe kill quotas count down from 170.</summary>
    KillRace = 4,

    /// <summary>A tribe has emptied its kill quota; waiting a fixed 3 real seconds to delete the battle scroll.</summary>
    ScrollPending = 5,

    /// <summary>The boss-kill window.</summary>
    BossWindow = 6,

    /// <summary>Boss confirmed dead; a fixed 1 real-minute cooldown before the return-to-town notice.</summary>
    PostWinCooldown = 7,

    /// <summary>A fixed 1 real-minute wait before every session on the map is force-disconnected and the cycle resets.</summary>
    PreReset = 8
}

/// <summary>Per-tick ambient inputs <see cref="ValleyWarSchedule.Tick" /> reads.</summary>
/// <param name="EligiblePlayerPresent">
///     Whether at least one ready, non-mid-transfer avatar is present on the map instance right now. The
///     contract's own third eligibility criterion ("not hiding") has no Fenrir stealth/hide state to check yet --
///     a documented gap, not a silent one (same posture <see cref="World.Monsters.MonsterAiSystem" />'s own
///     hidden-recheck gap already documents).
/// </param>
/// <param name="BossSlotOccupied">
///     Whether the reserved special-monster-object slot range the contract's §8b poll checks is still occupied.
///     Always <see langword="false" /> from every real caller in this cluster: the boss (monster id 756) is never
///     actually summoned here (see <see cref="ValleyWarSystem" />'s own remarks for the missing fixed-coordinate
///     citation), so the boss-kill window resolves as "already defeated" as soon as it is entered -- a documented
///     gap, not a silent one, same posture as <see cref="RegularWarEnvironmentSnapshot.BossMonsterAlive" />'s own
///     remarks for the sibling Zone049 boss-war map.
/// </param>
public readonly record struct ValleyWarEnvironmentSnapshot(bool EligiblePlayerPresent, bool BossSlotOccupied = false)
{
    public static ValleyWarEnvironmentSnapshot Empty { get; } = new(false);
}

/// <summary>Everything a caller needs to react to after one <see cref="ValleyWarSchedule.Tick" /> call.</summary>
public readonly record struct ValleyWarTickResult(
    ValleyWarPhase Phase,
    ValleyWarPhase PreviousPhase,
    int? GateCountdownValue = null,
    bool GateOpened = false,
    bool GateClosed = false,
    bool DoorOpened = false,
    int? DoorCountdownValue = null,
    ImmutableArray<int>? KillRaceQuotas = null,
    int? KillRaceCountdownValue = null,
    bool KillRaceEndedEmptyOrTimeout = false,
    bool TribeWin = false,
    byte? WinningTribe = null,
    bool BattleScrollDeleted = false,
    int? BossWindowCountdownValue = null,
    bool BossWindowTimeout = false,
    bool BossWin = false,
    bool PostWinReturnToTown = false,
    bool MonstersShouldDespawn = false,
    bool AllSessionsShouldDisconnect = false);

/// <summary>
///     One map instance's Valley of the Deceased (Zone 200/297/298/299) "Regular War" gate/door/kill-race/
///     boss-window/conclusion schedule. Pure and I/O-free -- driven entirely by <see cref="Tick" /> (one legacy
///     tick, ~500 ms, per call) and <see cref="RegisterMonsterKill" />; never touches a
///     <see cref="Zone" />/<see cref="PlayerRuntimeState" />/socket itself.
/// </summary>
/// <remarks>
///     <para>
///         <b>Distinct from <see cref="RegularWarSchedule" />.</b> Despite the superficial naming overlap ("Regular
///         War" is the legacy's own umbrella term for several unrelated periodic RvR state machines), this class
///         ports <c>Process_Zone_200</c> (<c>Server/ts25zone/S07_MyGame01.cpp:11149-11572</c>), gated to server
///         numbers 200/297/298/299 only (<c>:1116-1137</c>, <c>Server/ts25zone/H07_MyGame.h:36</c>) -- a completely
///         separate function, tSort family (659-669), and map set from <see cref="RegularWarSchedule" />'s own
///         <c>Process_Zone_049_TYPE</c> (<c>:4920-5521</c>, maps 49/146/149/154/157/160/120/121/122/295/164). Do
///         not route Zone049 events through this class or vice versa.
///     </para>
///     <para>
///         Réf. C++ (shared trigger/tick-unit context) : <c>Server/ts25zone/S07_MyGame01.cpp:2989-2994</c> (tick
///         call site) ; <c>:1116-1137</c> (eligibility flag) ; <c>:11149</c> (function definition) ;
///         <c>Server/ts25zone/H07_MyGame.h:36</c> (guarding macro is live) ; <c>Server/Header/function.h:1643-1652</c>
///         (120 ticks = 1 real minute, 7200 ticks = 1 real hour).
///     </para>
///     <para>
///         <b>Not modeled by this class</b> (left to the caller -- see <see cref="ValleyWarSystem" />'s own
///         remarks for the exact gaps and their citations): the client-facing broadcast/send itself, the general
///         kill-race monster summon population (no concrete monster id/count/coordinate data available in the
///         source contract), the boss (monster id 756) summon coordinate, the "animal5" randomized reward item,
///         and every ground-item/session-disconnect mutation.
///     </para>
/// </remarks>
public sealed class ValleyWarSchedule
{
    /// <summary>4 tribes -- same width as every other RvR system in this cluster.</summary>
    public const int TribeCount = 4;

    /// <summary>6 hours (Server/ts25zone/S07_MyGame01.cpp:11149-11572 preconditions, this cluster's own contract §2).</summary>
    public const int IdleWaitTicks = 43200;

    /// <summary>Seeded value for the gate-open countdown -- 5 sends, one per elapsed real minute (contract §2).</summary>
    public const int GateCountdownStartValue = 5;

    /// <summary>1 real minute per countdown step, and per the final gate-open transition tick (contract §2/§3).</summary>
    public const int GateCountdownIntervalTicks = 120;

    /// <summary>1 more real minute in the "gate open" phase before it closes (contract §4).</summary>
    public const int GateOpenTicks = 120;

    /// <summary>~10 real seconds (20 legacy ticks) before the door opens (contract §5).</summary>
    public const int DoorPendingTicks = 20;

    /// <summary>2 legacy ticks per real second -- the zone-local door countdown's own cadence (contract §11).</summary>
    public const int LegacyTicksPerRealSecond = 2;

    /// <summary>The zone-local door countdown steps 10 down to 1 (contract §11).</summary>
    public const int DoorCountdownStartValue = 10;

    /// <summary>30 real minutes (3600 legacy ticks) -- both the kill-race and boss-window timers (contract §5).</summary>
    public const int KillRaceDurationTicks = 3600;

    /// <summary>30 real minutes (3600 legacy ticks), armed alongside the kill-race timer but consumed later (contract §5).</summary>
    public const int BossWindowDurationTicks = 3600;

    /// <summary>Each tribe's kill quota at door-open (contract §5).</summary>
    public const int KillQuotaPerTribeStart = 170;

    /// <summary>3 real seconds (6 legacy ticks) before the battle scroll is deleted (contract §7).</summary>
    public const int ScrollDeleteDelayTicks = 6;

    /// <summary>1 real minute (120 legacy ticks) post-win cooldown before the return-to-town notice (contract §9).</summary>
    public const int PostWinCooldownTicks = 120;

    /// <summary>1 real minute (120 legacy ticks) before the forced disconnect + full reset (contract §10).</summary>
    public const int PreResetTicks = 120;

    /// <summary>
    ///     Legacy monster id for the boss summoned on a tribe kill-race win (contract §6b) -- retained here purely
    ///     for citation fidelity; this class never spawns it (no fixed-coordinate data available, see
    ///     <see cref="ValleyWarSystem" />'s own remarks).
    /// </summary>
    public const int BossMonsterId = 756;

    private readonly int[] _killRaceQuota = new int[TribeCount];

    private int _bossWindowEvenTickCounter;
    private int _bossWindowTickCounter;
    private int _bossWindowTicksArmed;
    private int _bossWindowTicksRemaining;
    private int _doorPendingTicksElapsed;
    private int _gateCountdownRemaining;
    private int _idleTicksElapsed;
    private int _killRaceEvenTickCounter;
    private int _killRaceTickCounter;
    private int _killRaceTicksRemaining;
    private int _minuteTicksElapsed;
    private int _postWinTicksElapsed;
    private int _preResetTicksElapsed;
    private int _scrollPendingTicksElapsed;

    public ValleyWarPhase Phase { get; private set; } = ValleyWarPhase.Idle;

    /// <summary>Non-null from the moment a kill-race win is determined (contract §6b) until the next full reset.</summary>
    public byte? WinningTribe { get; private set; }

    /// <summary>Advances by exactly one legacy tick (~500 ms).</summary>
    public ValleyWarTickResult Tick(ValleyWarEnvironmentSnapshot snapshot)
    {
        var previousPhase = Phase;

        int? gateCountdownValue = null;
        var gateOpened = false;
        var gateClosed = false;
        var doorOpened = false;
        int? doorCountdownValue = null;
        ImmutableArray<int>? killRaceQuotas = null;
        int? killRaceCountdownValue = null;
        var killRaceEndedEmptyOrTimeout = false;
        var tribeWin = false;
        var battleScrollDeleted = false;
        int? bossWindowCountdownValue = null;
        var bossWindowTimeout = false;
        var bossWin = false;
        var postWinReturnToTown = false;
        var monstersShouldDespawn = false;
        var allSessionsShouldDisconnect = false;

        switch (Phase)
        {
            case ValleyWarPhase.Idle:
                TickIdle();
                break;

            case ValleyWarPhase.GateCountdown:
                TickGateCountdown(ref gateCountdownValue, ref gateOpened);
                break;

            case ValleyWarPhase.GateOpen:
                TickGateOpen(ref gateClosed);
                break;

            case ValleyWarPhase.DoorPending:
                TickDoorPending(ref doorCountdownValue, ref doorOpened);
                break;

            case ValleyWarPhase.KillRace:
                TickKillRace(snapshot, ref killRaceQuotas, ref killRaceCountdownValue,
                    ref killRaceEndedEmptyOrTimeout, ref tribeWin, ref monstersShouldDespawn);
                break;

            case ValleyWarPhase.ScrollPending:
                TickScrollPending(ref battleScrollDeleted);
                break;

            case ValleyWarPhase.BossWindow:
                TickBossWindow(snapshot, ref bossWindowCountdownValue, ref bossWindowTimeout, ref bossWin);
                break;

            case ValleyWarPhase.PostWinCooldown:
                TickPostWinCooldown(ref postWinReturnToTown, ref monstersShouldDespawn);
                break;

            case ValleyWarPhase.PreReset:
                TickPreReset(ref allSessionsShouldDisconnect);
                break;
        }

        return new ValleyWarTickResult(
            Phase,
            previousPhase,
            gateCountdownValue,
            gateOpened,
            gateClosed,
            doorOpened,
            doorCountdownValue,
            killRaceQuotas,
            killRaceCountdownValue,
            killRaceEndedEmptyOrTimeout,
            tribeWin,
            tribeWin ? WinningTribe : null,
            battleScrollDeleted,
            bossWindowCountdownValue,
            bossWindowTimeout,
            bossWin,
            postWinReturnToTown,
            monstersShouldDespawn,
            allSessionsShouldDisconnect);
    }

    /// <summary>
    ///     One qualifying monster killing blow decrements <paramref name="tribeId" />'s kill quota by one,
    ///     floor-clamped at zero -- no-op outside <see cref="ValleyWarPhase.KillRace" />.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:3162-3170.</remarks>
    public void RegisterMonsterKill(byte tribeId)
    {
        if (Phase != ValleyWarPhase.KillRace || tribeId >= TribeCount)
            return;

        if (_killRaceQuota[tribeId] > 0)
            _killRaceQuota[tribeId]--;
    }

    /// <summary>
    ///     Immediately zeroes <paramref name="tribeId" />'s kill quota -- the effect of the legacy's unguarded,
    ///     unauthenticated "kill200" chat command (contract §6 Inputs). Exposed for completeness only: no caller
    ///     in this cluster wires an actual command to it -- porting a live, unauthenticated exploit surface needs
    ///     its own explicit decision, not a side effect of implementing this broadcast lifecycle. No-op outside
    ///     <see cref="ValleyWarPhase.KillRace" />.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7933-7939 (the unguarded command), contrasted with
    ///     :7905-7912 (the guarded sibling "boss"-spawn GM command's rank check).
    /// </remarks>
    public void ForceZeroTribeQuota(byte tribeId)
    {
        if (Phase != ValleyWarPhase.KillRace || tribeId >= TribeCount)
            return;

        _killRaceQuota[tribeId] = 0;
    }

    public int GetKillQuota(byte tribeId)
    {
        return tribeId < TribeCount ? _killRaceQuota[tribeId] : 0;
    }

    private void TickIdle()
    {
        _idleTicksElapsed++;
        if (_idleTicksElapsed < IdleWaitTicks)
            return;

        _idleTicksElapsed = 0;
        _gateCountdownRemaining = GateCountdownStartValue;
        _minuteTicksElapsed = 0;
        Phase = ValleyWarPhase.GateCountdown;
    }

    private void TickGateCountdown(ref int? gateCountdownValue, ref bool gateOpened)
    {
        _minuteTicksElapsed++;
        if (_minuteTicksElapsed < GateCountdownIntervalTicks)
            return;

        _minuteTicksElapsed = 0;

        if (_gateCountdownRemaining > 0)
        {
            gateCountdownValue = _gateCountdownRemaining;
            _gateCountdownRemaining--;
            return;
        }

        gateOpened = true;
        Phase = ValleyWarPhase.GateOpen;
    }

    private void TickGateOpen(ref bool gateClosed)
    {
        _minuteTicksElapsed++;
        if (_minuteTicksElapsed < GateOpenTicks)
            return;

        gateClosed = true;
        _doorPendingTicksElapsed = 0;
        Phase = ValleyWarPhase.DoorPending;
    }

    private void TickDoorPending(ref int? doorCountdownValue, ref bool doorOpened)
    {
        _doorPendingTicksElapsed++;

        if (_doorPendingTicksElapsed % LegacyTicksPerRealSecond == 0)
        {
            var secondsElapsed = _doorPendingTicksElapsed / LegacyTicksPerRealSecond;
            var remaining = DoorCountdownStartValue - secondsElapsed + 1;
            if (remaining is >= 1 and <= DoorCountdownStartValue)
                doorCountdownValue = remaining;
        }

        if (_doorPendingTicksElapsed < DoorPendingTicks)
            return;

        doorOpened = true;
        Array.Clear(_killRaceQuota);
        for (var t = 0; t < TribeCount; t++)
            _killRaceQuota[t] = KillQuotaPerTribeStart;

        _killRaceTicksRemaining = KillRaceDurationTicks;
        _bossWindowTicksArmed = BossWindowDurationTicks;
        _killRaceTickCounter = 0;
        _killRaceEvenTickCounter = 0;
        Phase = ValleyWarPhase.KillRace;
    }

    private void TickKillRace(ValleyWarEnvironmentSnapshot snapshot, ref ImmutableArray<int>? killRaceQuotas,
        ref int? killRaceCountdownValue, ref bool killRaceEndedEmptyOrTimeout, ref bool tribeWin,
        ref bool monstersShouldDespawn)
    {
        if (!snapshot.EligiblePlayerPresent)
        {
            killRaceEndedEmptyOrTimeout = true;
            monstersShouldDespawn = true;
            WinningTribe = null;
            EnterPreReset();
            return;
        }

        if (_killRaceTicksRemaining > 0)
            _killRaceTicksRemaining--;

        _killRaceTickCounter++;
        if (_killRaceTickCounter % 2 == 0)
        {
            killRaceQuotas = ImmutableArray.Create(_killRaceQuota[0], _killRaceQuota[1], _killRaceQuota[2],
                _killRaceQuota[3]);

            _killRaceEvenTickCounter++;
            if (_killRaceEvenTickCounter % 5 == 0)
                killRaceCountdownValue = _killRaceTicksRemaining;
        }

        for (byte t = 0; t < TribeCount; t++)
            if (_killRaceQuota[t] <= 0)
            {
                tribeWin = true;
                WinningTribe = t;
                monstersShouldDespawn = true;
                _scrollPendingTicksElapsed = 0;
                Phase = ValleyWarPhase.ScrollPending;
                return;
            }

        if (_killRaceTicksRemaining <= 0)
        {
            killRaceEndedEmptyOrTimeout = true;
            monstersShouldDespawn = true;
            WinningTribe = null;
            EnterPreReset();
        }
    }

    private void TickScrollPending(ref bool battleScrollDeleted)
    {
        _scrollPendingTicksElapsed++;
        if (_scrollPendingTicksElapsed < ScrollDeleteDelayTicks)
            return;

        battleScrollDeleted = true;
        _bossWindowTicksRemaining = _bossWindowTicksArmed;
        _bossWindowTickCounter = 0;
        _bossWindowEvenTickCounter = 0;
        Phase = ValleyWarPhase.BossWindow;
    }

    private void TickBossWindow(ValleyWarEnvironmentSnapshot snapshot, ref int? bossWindowCountdownValue,
        ref bool bossWindowTimeout, ref bool bossWin)
    {
        if (_bossWindowTicksRemaining > 0)
            _bossWindowTicksRemaining--;

        _bossWindowTickCounter++;
        if (_bossWindowTickCounter % 2 == 0)
        {
            _bossWindowEvenTickCounter++;
            if (_bossWindowEvenTickCounter % 5 == 0)
                bossWindowCountdownValue = _bossWindowTicksRemaining;
        }

        if (!snapshot.BossSlotOccupied)
        {
            bossWin = true;
            _postWinTicksElapsed = 0;
            Phase = ValleyWarPhase.PostWinCooldown;
            return;
        }

        if (_bossWindowTicksRemaining <= 0)
        {
            bossWindowTimeout = true;
            EnterPreReset();
        }
    }

    private void TickPostWinCooldown(ref bool postWinReturnToTown, ref bool monstersShouldDespawn)
    {
        _postWinTicksElapsed++;
        if (_postWinTicksElapsed < PostWinCooldownTicks)
            return;

        postWinReturnToTown = true;
        monstersShouldDespawn = true;
        EnterPreReset();
    }

    private void TickPreReset(ref bool allSessionsShouldDisconnect)
    {
        _preResetTicksElapsed++;
        if (_preResetTicksElapsed < PreResetTicks)
            return;

        allSessionsShouldDisconnect = true;

        Phase = ValleyWarPhase.Idle;
        _idleTicksElapsed = 0;
        _gateCountdownRemaining = 0;
        _minuteTicksElapsed = 0;
        _doorPendingTicksElapsed = 0;
        Array.Clear(_killRaceQuota);
        _killRaceTicksRemaining = 0;
        _killRaceTickCounter = 0;
        _killRaceEvenTickCounter = 0;
        _scrollPendingTicksElapsed = 0;
        _bossWindowTicksArmed = 0;
        _bossWindowTicksRemaining = 0;
        _bossWindowTickCounter = 0;
        _bossWindowEvenTickCounter = 0;
        _postWinTicksElapsed = 0;
        WinningTribe = null;
    }

    private void EnterPreReset()
    {
        Phase = ValleyWarPhase.PreReset;
        _preResetTicksElapsed = 0;
    }
}
