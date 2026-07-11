using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public sealed class Zone175MissionCoreTests
{
    private const int OneMinute = Zone175RewardTables.OneMinuteLegacyTicks;

    private static DateTimeOffset NextSunday2100()
    {
        var candidate = new DateTimeOffset(2026, 7, 12, 21, 0, 0, TimeSpan.Zero);
        while (candidate.DayOfWeek != DayOfWeek.Sunday)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    private static Zone175InstanceConfig Config(int index2 = 4)
    {
        return new Zone175InstanceConfig(0, index2, 1f, 1f);
    }

    [Fact]
    public void IsOpenMoment_OnlyTrueAtSunday2100Exactly()
    {
        var sunday2100 = NextSunday2100();

        Assert.True(Zone175MissionCore.IsOpenMoment(sunday2100));
        Assert.False(Zone175MissionCore.IsOpenMoment(sunday2100.AddMinutes(1))); // 21:01
        Assert.False(Zone175MissionCore.IsOpenMoment(sunday2100.AddHours(1))); // 22:00
        Assert.False(Zone175MissionCore.IsOpenMoment(sunday2100.AddDays(1))); // Monday 21:00
        Assert.False(Zone175MissionCore.IsOpenMoment(sunday2100.AddHours(-1))); // 20:00
    }

    [Fact]
    public void Idle_OffSchedule_StaysIdleAndSilent()
    {
        var state = new Zone175MissionState();
        var config = Config();
        var effects = new RecordingZone175MissionEffects();
        var monday = NextSunday2100().AddDays(1);

        Zone175MissionCore.Advance(state, in config, effects, monday, 1);

        Assert.Equal(Zone175MissionPhase.Idle, state.Phase);
        Assert.Empty(effects.Events);
    }

    [Fact]
    public void NonPositiveTickBatch_IsANoOp()
    {
        var state = new Zone175MissionState();
        var config = Config();
        var effects = new RecordingZone175MissionEffects();

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 0);
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), -5);

        Assert.Equal(Zone175MissionPhase.Idle, state.Phase);
        Assert.Equal(0, state.SubTick);
        Assert.Empty(effects.Events);
    }

    [Fact]
    public void Idle_AtOpenMoment_OpensOnceWithCountdownTenAndStartLog()
    {
        var state = new Zone175MissionState();
        var config = Config();
        var effects = new RecordingZone175MissionEffects();

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(Zone175MissionPhase.PreOpen, state.Phase);
        Assert.Equal(Zone175RewardTables.PreOpenCountStart, state.PreOpenRemaining);
        var opened = Assert.Single(effects.Events);
        Assert.Equal(Zone175MissionEvent.MissionOpen, opened.Event);
        Assert.Equal(10, opened.Remaining);
    }

    [Fact]
    public void Idle_DoesNotReopenTheSameDay()
    {
        var state = new Zone175MissionState();
        var config = Config();
        var effects = new RecordingZone175MissionEffects();
        var sunday2100 = NextSunday2100();

        Zone175MissionCore.Advance(state, in config, effects, sunday2100, 1);
        Assert.Equal(Zone175MissionPhase.PreOpen, state.Phase);

        // Simulate the machine having returned to Idle later that same Sunday (e.g. a short prior cycle): the
        // once-per-day guard must refuse to reopen at the same 21:00 minute.
        state.Phase = Zone175MissionPhase.Idle;
        effects.Events.Clear();

        Zone175MissionCore.Advance(state, in config, effects, sunday2100, 1);

        Assert.Equal(Zone175MissionPhase.Idle, state.Phase);
        Assert.Empty(effects.Events);
    }

    [Fact]
    public void PreOpen_CountsDownOncePerMinuteThenBeginsWaveOne()
    {
        var state = new Zone175MissionState();
        var config = Config();
        var effects = new RecordingZone175MissionEffects();
        var now = NextSunday2100();

        Zone175MissionCore.Advance(state, in config, effects, now, 1); // open
        effects.Events.Clear();

        // One minute -> one decrement (10 -> 9).
        Zone175MissionCore.Advance(state, in config, effects, now, OneMinute);
        var first = Assert.Single(effects.Events);
        Assert.Equal(Zone175MissionEvent.PreOpenCountdown, first.Event);
        Assert.Equal(9, first.Remaining);
        Assert.Equal(Zone175MissionPhase.PreOpen, state.Phase);

        // The remaining nine minutes' worth in one batch -> nine more decrements down to 0 -> begin wave 1.
        Zone175MissionCore.Advance(state, in config, effects, now, 9 * OneMinute);

        Assert.Equal(Zone175MissionPhase.WaveBossSummon, state.Phase);
        Assert.Equal(1, state.CurrentWave);
        Assert.Equal(0, state.PreOpenRemaining);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.WaveGateOpen && e.Wave == 1);
    }

    [Fact]
    public void BossSummon_SummonsWaveBossThenEntersCombat()
    {
        var (state, config, effects) = OpenAndReachWaveOneSummon();

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(Zone175MissionPhase.WaveCombat, state.Phase);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.WaveBossSummon && e.Wave == 1);
        Assert.Equal(new[] { 1 }, effects.SummonedBosses);
    }

    [Fact]
    public void Combat_BossStillAlive_KeepsRunningAndTricklesEvery20SubTicks()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 1);

        // 20 sub-ticks -> exactly one trickle summon; boss still alive so no clear.
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 20);
        Assert.Equal(Zone175MissionPhase.WaveCombat, state.Phase);
        Assert.Single(effects.TrickleSummons);

        // 45 more -> two further trickles (45/20), remainder carried.
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 45);
        Assert.Equal(3, effects.TrickleSummons.Count);
        Assert.All(effects.TrickleSummons, stage => Assert.Equal(1, stage));
    }

    [Fact]
    public void Combat_BossCleared_RemovesMonstersRewardsAndAdvancesWhenDepthAllows()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 0);

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(1, effects.RemoveMissionMonstersCount);
        Assert.Equal(new[] { 1 }, effects.Rewards);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.WaveCleared && e.Wave == 1);
        // index2 = 4 allows advancing to wave 2.
        Assert.Equal(Zone175MissionPhase.WaveBossSummon, state.Phase);
        Assert.Equal(2, state.CurrentWave);
    }

    [Fact]
    public void Combat_BossCleared_DepthGateStopsProgressionForLowIndex2()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 0, index2: 0);

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(new[] { 1 }, effects.Rewards);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.DepthGateStop);
        Assert.DoesNotContain(effects.Events, e => e.Event == Zone175MissionEvent.MissionEnd);
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);
    }

    [Fact]
    public void FifthWaveClear_WritesMissionEndAndEntersTerminal()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 0, index2: 4);

        // Clear all five waves in sequence.
        for (var wave = 1; wave <= Zone175RewardTables.WaveCount; wave++)
        {
            if (state.Phase == Zone175MissionPhase.WaveBossSummon)
                Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1); // -> combat
            Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1); // clear
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, effects.Rewards);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.MissionEnd && e.Wave == 5);
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);
    }

    [Fact]
    public void Combat_NoQualifyingPlayer_EmptyAbortsToTerminal()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 1);
        effects.PlayerPresent = false;

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(1, effects.RemoveMissionMonstersCount);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.EmptyAbort);
        Assert.Empty(effects.Rewards);
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);
    }

    [Fact]
    public void Combat_TimeoutAbortsToTerminal()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 1);

        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(),
            Zone175RewardTables.WaveTimeoutLegacyTicks);

        Assert.Equal(1, effects.RemoveMissionMonstersCount);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.WaveTimeout);
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);
    }

    [Fact]
    public void Terminal_AfterHold_KicksEveryoneAndResetsToIdle()
    {
        var (state, config, effects) = OpenAndReachWaveOneCombat(livingBosses: 1);
        effects.PlayerPresent = false;
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1); // -> Terminal
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);

        // Not yet elapsed: still holding.
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(),
            Zone175RewardTables.TerminalHoldLegacyTicks - 1);
        Assert.Equal(Zone175MissionPhase.Terminal, state.Phase);
        Assert.Equal(0, effects.ForceDisconnectAllCount);

        // Cross the 60-minute hold.
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1);

        Assert.Equal(1, effects.ForceDisconnectAllCount);
        Assert.Contains(effects.Events, e => e.Event == Zone175MissionEvent.TerminalKickReset);
        Assert.Equal(Zone175MissionPhase.Idle, state.Phase);
        Assert.Equal(0, state.CurrentWave);
    }

    private static (Zone175MissionState State, Zone175InstanceConfig Config, RecordingZone175MissionEffects Effects)
        OpenAndReachWaveOneSummon(int index2 = 4)
    {
        var state = new Zone175MissionState();
        var config = Config(index2);
        var effects = new RecordingZone175MissionEffects();
        var now = NextSunday2100();

        Zone175MissionCore.Advance(state, in config, effects, now, 1); // open
        Zone175MissionCore.Advance(state, in config, effects, now, 10 * OneMinute); // countdown -> wave 1 summon
        effects.Events.Clear();
        return (state, config, effects);
    }

    private static (Zone175MissionState State, Zone175InstanceConfig Config, RecordingZone175MissionEffects Effects)
        OpenAndReachWaveOneCombat(int livingBosses, int index2 = 4)
    {
        var (state, config, effects) = OpenAndReachWaveOneSummon(index2);
        effects.LivingBosses = livingBosses;
        Zone175MissionCore.Advance(state, in config, effects, NextSunday2100(), 1); // summon -> combat
        effects.Events.Clear();
        effects.SummonedBosses.Clear();
        return (state, config, effects);
    }
}

/// <summary>Records every <see cref="IZone175MissionEffects" /> call and answers queries from settable fields.</summary>
internal sealed class RecordingZone175MissionEffects : IZone175MissionEffects
{
    public bool PlayerPresent { get; set; } = true;
    public int LivingBosses { get; set; }
    public List<(Zone175MissionEvent Event, int Wave, int Remaining)> Events { get; } = [];
    public List<int> SummonedBosses { get; } = [];
    public List<int> TrickleSummons { get; } = [];
    public List<int> Rewards { get; } = [];
    public int RemoveMissionMonstersCount { get; private set; }
    public int ForceDisconnectAllCount { get; private set; }

    public bool AnyQualifyingPlayerPresent()
    {
        return PlayerPresent;
    }

    public int CountLivingWaveBosses(int stage)
    {
        return LivingBosses;
    }

    public void SummonWaveBoss(int stage)
    {
        SummonedBosses.Add(stage);
    }

    public void SummonTrickle(int stage)
    {
        TrickleSummons.Add(stage);
    }

    public void RemoveMissionMonsters()
    {
        RemoveMissionMonstersCount++;
    }

    public void RewardQualifyingPlayers(int stage)
    {
        Rewards.Add(stage);
    }

    public void ForceDisconnectAll()
    {
        ForceDisconnectAllCount++;
    }

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
        Events.Add((missionEvent, wave, remaining));
    }
}
