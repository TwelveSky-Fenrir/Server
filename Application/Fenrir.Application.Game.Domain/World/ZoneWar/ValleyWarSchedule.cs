using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum ValleyWarPhase : byte
{

        Idle = 0,

        GateCountdown = 1,

        GateOpen = 2,

        DoorPending = 3,

        KillRace = 4,

        ScrollPending = 5,

        BossWindow = 6,

        PostWinCooldown = 7,

        PreReset = 8
}

public readonly record struct ValleyWarEnvironmentSnapshot(bool EligiblePlayerPresent, bool BossSlotOccupied = false)
{
    public static ValleyWarEnvironmentSnapshot Empty { get; } = new(false);
}

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

public sealed class ValleyWarSchedule
{

        public const int TribeCount = 4;

        public const int IdleWaitTicks = 43200;

        public const int GateCountdownStartValue = 5;

        public const int GateCountdownIntervalTicks = 120;

        public const int GateOpenTicks = 120;

        public const int DoorPendingTicks = 20;

        public const int LegacyTicksPerRealSecond = 2;

        public const int DoorCountdownStartValue = 10;

        public const int KillRaceDurationTicks = 3600;

        public const int BossWindowDurationTicks = 3600;

        public const int KillQuotaPerTribeStart = 170;

        public const int ScrollDeleteDelayTicks = 6;

        public const int PostWinCooldownTicks = 120;

        public const int PreResetTicks = 120;

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

        public byte? WinningTribe { get; private set; }

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

        public void RegisterMonsterKill(byte tribeId)
    {
        if (Phase != ValleyWarPhase.KillRace || tribeId >= TribeCount)
            return;

        if (_killRaceQuota[tribeId] > 0)
            _killRaceQuota[tribeId]--;
    }

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
