using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum RegularWarPhase : byte
{
    Idle = 0,

    OpenGate = 1,

    PreWar = 2,

    Active = 3,

    PostWarCleanup = 4,

    ForcedReset = 5
}

public enum RegularWarOutcome : byte
{
    Draw,

    TribeWin,

    AbortedEmptyMap
}

public readonly record struct RegularWarEnvironmentSnapshot(
    int TotalPresentCount,
    ImmutableArray<int> PresentCountByTribe,
    bool BossMonsterAlive = false)
{
    public static RegularWarEnvironmentSnapshot Empty { get; } = new(0, ImmutableArray.Create(0, 0, 0, 0));
}

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
    public bool EnteredActiveWar => PreviousPhase == RegularWarPhase.PreWar && Phase == RegularWarPhase.Active;
}

public sealed class RegularWarSchedule(RegularWarMapConfig config)
{
    public const int TribeCount = WorldStateService.TribeCount;

    public const int CooldownTicks = 4800;

    public const int CountdownAnnounceIntervalTicks = 120;

    public const int CountdownAnnounceStartValue = 10;

    public const int FinalWaitTicks = 120;

    public const int OpenGateTicks = 360;

    public const int PreWarTicks = 120;

    // 900 ticks de simulation, pas 900 secondes - Server/ts25zone/S07_MyGame01.cpp:4588,4765,4801
    public const int ActiveWarDurationTicks = 900;

    public const int ActiveEvaluationCadenceTicks = 10;

    public const int PostWarCleanupTicks = 180;

    public const int BossPollMaxTicks = 1800;

    public const int BossConfirmedAbsenceTicks = 180;

    public const int ForcedResetDisconnectAtTicks = 12;

    public const int ForcedResetHeartbeatTicks = 120;
    private readonly Dictionary<int, int> _killCountByCharacter = new();
    private readonly List<int> _killOrderSeen = [];

    private readonly int[] _tribeKillTally = new int[TribeCount];
    private int _activeEvaluationTicksElapsed;
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

    public int RemainingActiveWarTicks { get; private set; }

    public int WarCycleNumber { get; private set; } = 1;

    public byte? WinningTribe { get; private set; }

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
                // le legacy n'attend que si mRegularWarNumber > 0 : la 1re manche part sans cooldown
                // Server/ts25zone/S07_MyGame01.cpp:4645
                if (WarCycleNumber > 1 && _cooldownTicksElapsed < CooldownTicks)
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
            return;

        outcome = determined;
        monstersShouldDespawn = true;
        Phase = RegularWarPhase.PostWarCleanup;
        _postWarTicksElapsed = 0;
        _bossPollTicksElapsed = 0;
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

        _bossPollTicksElapsed++;
        if (_bossPollTicksElapsed < BossPollMaxTicks)
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

        RemainingActiveWarTicks = 0;
    }

    private void EnterForcedReset()
    {
        Phase = RegularWarPhase.ForcedReset;
        _forcedResetTicksElapsed = 0;
    }

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
                    WinningTribe = null;
                    return RegularWarOutcome.Draw;
                }

                winner = t;
            }

        WinningTribe = winner;
        return RegularWarOutcome.TribeWin;
    }

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
                return null;
        }
    }

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
