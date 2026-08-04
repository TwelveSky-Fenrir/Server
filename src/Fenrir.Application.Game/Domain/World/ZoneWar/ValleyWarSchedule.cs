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

public sealed record ValleyWarScheduleState(
    ValleyWarPhase Phase,
    byte? WinningTribe,
    int[] KillRaceQuotas,
    bool BossObservedPresent,
    int BossWindowTicksArmed,
    int BossWindowTicksRemaining,
    int DoorPendingTicksElapsed,
    int GateCountdownRemaining,
    int IdleTicksElapsed,
    int KillRaceTicksRemaining,
    int MinuteTicksElapsed,
    int PostWinTicksElapsed,
    int PreResetTicksElapsed,
    int ScrollPendingTicksElapsed);

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

    private readonly object _sync = new();

    private readonly int[] _killRaceQuota = new int[TribeCount];

    private bool _bossObservedPresent;

    private int _bossWindowTicksArmed;
    private int _bossWindowTicksRemaining;
    private int _doorPendingTicksElapsed;
    private int _gateCountdownRemaining;
    private int _idleTicksElapsed;
    private int _killRaceTicksRemaining;
    private int _minuteTicksElapsed;
    private int _postWinTicksElapsed;
    private int _preResetTicksElapsed;
    private int _scrollPendingTicksElapsed;

    public ValleyWarPhase Phase
    {
        get
        {
            lock (_sync)
                return _phase;
        }
        private set => _phase = value;
    }

    public byte? WinningTribe
    {
        get
        {
            lock (_sync)
                return _winningTribe;
        }
        private set => _winningTribe = value;
    }

    private ValleyWarPhase _phase = ValleyWarPhase.Idle;

    private byte? _winningTribe;

    public ValleyWarTickResult Tick(ValleyWarEnvironmentSnapshot snapshot)
    {
        lock (_sync)
            return TickCore(snapshot);
    }

    private ValleyWarTickResult TickCore(ValleyWarEnvironmentSnapshot snapshot)
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
                TickScrollPending(snapshot, ref battleScrollDeleted);
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

    public bool RegisterMonsterKill(byte tribeId)
    {
        lock (_sync)
        {
            if (Phase != ValleyWarPhase.KillRace || tribeId >= TribeCount)
                return false;

            if (_killRaceQuota[tribeId] > 0)
                _killRaceQuota[tribeId]--;

            return true;
        }
    }

    public void ForceZeroTribeQuota(byte tribeId)
    {
        lock (_sync)
        {
            if (Phase != ValleyWarPhase.KillRace || tribeId >= TribeCount)
                return;

            _killRaceQuota[tribeId] = 0;
        }
    }

    public int GetKillQuota(byte tribeId)
    {
        lock (_sync)
            return tribeId < TribeCount ? _killRaceQuota[tribeId] : 0;
    }

    public ValleyWarScheduleState Snapshot()
    {
        lock (_sync)
        {
            return new ValleyWarScheduleState(
                _phase,
                _winningTribe,
                (int[])_killRaceQuota.Clone(),
                _bossObservedPresent,
                _bossWindowTicksArmed,
                _bossWindowTicksRemaining,
                _doorPendingTicksElapsed,
                _gateCountdownRemaining,
                _idleTicksElapsed,
                _killRaceTicksRemaining,
                _minuteTicksElapsed,
                _postWinTicksElapsed,
                _preResetTicksElapsed,
                _scrollPendingTicksElapsed);
        }
    }

    public void Restore(ValleyWarScheduleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateState(state);

        lock (_sync)
        {
            _phase = state.Phase;
            _winningTribe = state.WinningTribe;
            state.KillRaceQuotas.CopyTo(_killRaceQuota, 0);
            _bossObservedPresent = state.BossObservedPresent;
            _bossWindowTicksArmed = state.BossWindowTicksArmed;
            _bossWindowTicksRemaining = state.BossWindowTicksRemaining;
            _doorPendingTicksElapsed = state.DoorPendingTicksElapsed;
            _gateCountdownRemaining = state.GateCountdownRemaining;
            _idleTicksElapsed = state.IdleTicksElapsed;
            _killRaceTicksRemaining = state.KillRaceTicksRemaining;
            _minuteTicksElapsed = state.MinuteTicksElapsed;
            _postWinTicksElapsed = state.PostWinTicksElapsed;
            _preResetTicksElapsed = state.PreResetTicksElapsed;
            _scrollPendingTicksElapsed = state.ScrollPendingTicksElapsed;
        }
    }

    private void TickIdle()
    {
        _idleTicksElapsed++;
        if (_idleTicksElapsed < IdleWaitTicks)
            return;

        _idleTicksElapsed = 0;
        _gateCountdownRemaining = GateCountdownStartValue;
        _minuteTicksElapsed = GateCountdownIntervalTicks;
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

        _killRaceTicksRemaining--;
        if (_killRaceTicksRemaining % 2 != 0)
            return;

        killRaceQuotas = ImmutableArray.Create(_killRaceQuota[0], _killRaceQuota[1], _killRaceQuota[2],
            _killRaceQuota[3]);

        if (_killRaceTicksRemaining % 10 == 0)
            killRaceCountdownValue = _killRaceTicksRemaining / LegacyTicksPerRealSecond;

        if (_killRaceTicksRemaining <= 0)
        {
            killRaceEndedEmptyOrTimeout = true;
            monstersShouldDespawn = true;
            WinningTribe = null;
            EnterPreReset();
            return;
        }

        for (byte t = 0; t < TribeCount; t++)
            if (_killRaceQuota[t] <= 0)
            {
                tribeWin = true;
                WinningTribe = t;
                monstersShouldDespawn = true;
                _scrollPendingTicksElapsed = 0;
                _bossObservedPresent = false;
                Phase = ValleyWarPhase.ScrollPending;
                return;
            }
    }

    private void TickScrollPending(ValleyWarEnvironmentSnapshot snapshot, ref bool battleScrollDeleted)
    {
        if (snapshot.BossSlotOccupied)
            _bossObservedPresent = true;

        _scrollPendingTicksElapsed++;
        if (_scrollPendingTicksElapsed < ScrollDeleteDelayTicks)
            return;

        battleScrollDeleted = true;
        _bossWindowTicksRemaining = _bossWindowTicksArmed;
        Phase = ValleyWarPhase.BossWindow;
    }

    private void TickBossWindow(ValleyWarEnvironmentSnapshot snapshot, ref int? bossWindowCountdownValue,
        ref bool bossWindowTimeout, ref bool bossWin)
    {
        if (snapshot.BossSlotOccupied)
            _bossObservedPresent = true;

        _bossWindowTicksRemaining--;
        if (_bossWindowTicksRemaining % 2 != 0)
            return;

        if (_bossWindowTicksRemaining % 10 == 0)
            bossWindowCountdownValue = _bossWindowTicksRemaining / LegacyTicksPerRealSecond;

        if (_bossWindowTicksRemaining <= 0)
        {
            bossWindowTimeout = true;
            EnterPreReset();
            return;
        }

        if (snapshot.BossSlotOccupied)
            return;

        if (!_bossObservedPresent)
            return;

        bossWin = true;
        _postWinTicksElapsed = 0;
        Phase = ValleyWarPhase.PostWinCooldown;
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
        _scrollPendingTicksElapsed = 0;
        _bossWindowTicksArmed = 0;
        _bossWindowTicksRemaining = 0;
        _bossObservedPresent = false;
        _postWinTicksElapsed = 0;
        WinningTribe = null;
    }

    private void EnterPreReset()
    {
        Phase = ValleyWarPhase.PreReset;
        _preResetTicksElapsed = 0;
    }

    private static void ValidateState(ValleyWarScheduleState state)
    {
        if (!Enum.IsDefined(state.Phase) || state.WinningTribe is { } winner && winner >= TribeCount ||
            state.KillRaceQuotas is not { Length: TribeCount })
            throw new ArgumentException("The valley-war schedule snapshot has an invalid shape.", nameof(state));

        foreach (var quota in state.KillRaceQuotas)
            if (quota is < 0 or > KillQuotaPerTribeStart)
                throw new ArgumentException("The valley-war schedule snapshot has an invalid quota.", nameof(state));

        if (state.BossWindowTicksArmed < 0 || state.BossWindowTicksRemaining < 0 ||
            state.DoorPendingTicksElapsed < 0 || state.GateCountdownRemaining is < 0 or > GateCountdownStartValue ||
            state.IdleTicksElapsed < 0 || state.KillRaceTicksRemaining < 0 || state.MinuteTicksElapsed < 0 ||
            state.PostWinTicksElapsed < 0 || state.PreResetTicksElapsed < 0 || state.ScrollPendingTicksElapsed < 0)
            throw new ArgumentException("The valley-war schedule snapshot contains a negative counter.", nameof(state));
    }
}
