namespace Fenrir.Application.Game.Domain.Simulation;

public static class Zone175MissionCore
{
    private const int CountdownTicks = SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int OpenGateTicks = 3 * SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int StageOpenTicks = SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int StageCloseTicks = 2 * SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int StageSummonTicks = SimulationClock.PlayTimeAccrualLegacyTicks;
    private const int WaveTimeoutTicks = 60 * SimulationClock.PlayTimeAccrualLegacyTicks;

    public static void Advance(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, DateTimeOffset nowLocal, int sharedState, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        for (var tick = 0; tick < legacyTicksElapsed; tick++)
            AdvanceOneTick(state, in config, effects, nowLocal, sharedState);
    }

    public static bool IsOpenMoment(DateTimeOffset nowLocal)
    {
        return nowLocal.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday or DayOfWeek.Sunday &&
               nowLocal.Hour == 22 && nowLocal.Minute == 0;
    }

    private static void AdvanceOneTick(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, DateTimeOffset nowLocal, int sharedState)
    {
        if (sharedState is < 0 or > 23)
            return;

        if (state.SharedState != sharedState)
        {
            state.SharedState = sharedState;
            state.StateTicks = 0;
            state.StageLoadBlocked = false;
            if (sharedState is not (5 or 9 or 13 or 17 or 21))
            {
                state.StageLoaded = false;
                state.LoadedStage = 0;
            }
        }

        state.SubTick++;
        state.StateTicks++;
        state.Phase = ToPhase(sharedState, state.IdleBattleState);

        switch (sharedState)
        {
            case 0:
                AdvanceIdle(state, effects, nowLocal);
                return;
            case 1:
                AdvanceAfterDelay(state, effects, OpenGateTicks, 65);
                return;
            case 2:
                AdvanceAfterDelay(state, effects, StageOpenTicks, 66);
                return;
            case 3:
                AdvanceAfterDelay(state, effects, StageCloseTicks, 67);
                return;
            case 4:
                BeginStage(state, effects, 1, 68);
                return;
            case 5:
                AdvanceWave(state, effects, 1, 69, 70, 71);
                return;
            case 6:
                AdvanceDepthGate(state, in config, effects, 1, 72, 73);
                return;
            case 7:
                AdvanceAfterDelay(state, effects, StageCloseTicks, 74);
                return;
            case 8:
                BeginStage(state, effects, 2, 75);
                return;
            case 9:
                AdvanceWave(state, effects, 2, 76, 77, 78);
                return;
            case 10:
                AdvanceDepthGate(state, in config, effects, 2, 79, 80);
                return;
            case 11:
                AdvanceAfterDelay(state, effects, StageCloseTicks, 81);
                return;
            case 12:
                BeginStage(state, effects, 3, 82);
                return;
            case 13:
                AdvanceWave(state, effects, 3, 83, 84, 85);
                return;
            case 14:
                AdvanceDepthGate(state, in config, effects, 3, 86, 87);
                return;
            case 15:
                AdvanceAfterDelay(state, effects, StageCloseTicks, 88);
                return;
            case 16:
                BeginStage(state, effects, 4, 89);
                return;
            case 17:
                AdvanceWave(state, effects, 4, 90, 91, 92);
                return;
            case 18:
                AdvanceDepthGate(state, in config, effects, 4, 93, 94);
                return;
            case 19:
                AdvanceAfterDelay(state, effects, StageCloseTicks, 95);
                return;
            case 20:
                BeginStage(state, effects, 5, 96);
                return;
            case 21:
                AdvanceWave(state, effects, 5, 97, 98, 99);
                return;
            case 22:
                AdvanceAfterDelay(state, effects, StageOpenTicks, 100);
                return;
            case 23:
                AdvanceTerminal(state, effects);
                return;
        }
    }

    private static void AdvanceIdle(Zone175MissionState state, IZone175MissionEffects effects,
        DateTimeOffset nowLocal)
    {
        switch (state.IdleBattleState)
        {
            case 0:
            {
                var today = DateOnly.FromDateTime(nowLocal.DateTime);
                if (!IsOpenMoment(nowLocal) || state.LastScheduledDateLocal == today)
                    return;

                state.LastScheduledDateLocal = today;
                state.IdleBattleState = 1;
                state.CountdownRemaining = 10;
                state.StateTicks = CountdownTicks;
                effects.Notify(Zone175MissionEvent.MissionOpen, 0, state.CountdownRemaining);
                return;
            }
            case 1:
                if (state.StateTicks < CountdownTicks)
                    return;

                state.StateTicks = 0;
                if (state.CountdownRemaining > 0)
                    effects.PublishStateChange(63, state.CountdownRemaining);
                state.CountdownRemaining--;
                if (state.CountdownRemaining < 1)
                    state.IdleBattleState = 2;
                return;
            case 2:
                if (state.StateTicks < CountdownTicks)
                    return;

                state.StateTicks = 0;
                state.IdleBattleState = 0;
                effects.PublishStateChange(64);
                return;
        }
    }

    private static void AdvanceAfterDelay(Zone175MissionState state, IZone175MissionEffects effects,
        int requiredTicks, int eventCode)
    {
        if (state.StateTicks < requiredTicks)
            return;

        state.StateTicks = 0;
        effects.PublishStateChange(eventCode);
    }

    private static void BeginStage(Zone175MissionState state, IZone175MissionEffects effects, int stage,
        int eventCode)
    {
        if (state.StateTicks < StageSummonTicks || state.StageLoadBlocked)
            return;

        if (!effects.TryLoadWaveStage(stage))
        {
            state.StageLoadBlocked = true;
            return;
        }

        state.StageLoaded = true;
        state.LoadedStage = stage;
        state.StateTicks = 0;
        effects.Notify(Zone175MissionEvent.WaveBossSummon, stage, 0);
        effects.PublishStateChange(eventCode);
    }

    private static void AdvanceWave(Zone175MissionState state, IZone175MissionEffects effects, int stage,
        int emptyEventCode, int timeoutEventCode, int clearedEventCode)
    {
        if (!EnsureWaveLoaded(state, effects, stage))
            return;

        if (!effects.AnyQualifyingPlayerPresent())
        {
            EndWave(state, effects, stage, emptyEventCode, Zone175MissionEvent.EmptyAbort, false);
            return;
        }

        if (state.StateTicks == WaveTimeoutTicks)
        {
            EndWave(state, effects, stage, timeoutEventCode, Zone175MissionEvent.WaveTimeout, false);
            return;
        }

        if (state.StateTicks % SimulationClock.MonsterRespawnScanLegacyTicks == 0)
            effects.MaintainWaveStage();

        if (effects.CountLivingWaveBosses(stage) != 0)
            return;

        EndWave(state, effects, stage, clearedEventCode, Zone175MissionEvent.WaveCleared, true);
    }

    private static bool EnsureWaveLoaded(Zone175MissionState state, IZone175MissionEffects effects, int stage)
    {
        if (state.StageLoaded && state.LoadedStage == stage)
            return true;

        if (state.StageLoadBlocked)
            return false;

        if (!effects.TryLoadWaveStage(stage))
        {
            state.StageLoadBlocked = true;
            return false;
        }

        state.StageLoaded = true;
        state.LoadedStage = stage;
        effects.Notify(Zone175MissionEvent.WaveBossSummon, stage, 0);
        return true;
    }

    private static void EndWave(Zone175MissionState state, IZone175MissionEffects effects, int stage,
        int eventCode, Zone175MissionEvent missionEvent, bool award)
    {
        effects.RemoveMissionMonsters();
        if (award)
            effects.RewardQualifyingPlayers(stage);
        state.StageLoaded = false;
        state.LoadedStage = 0;
        state.StateTicks = 0;
        effects.Notify(missionEvent, stage, 0);
        effects.PublishStateChange(eventCode);
    }

    private static void AdvanceDepthGate(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, int completedStage, int deniedEventCode, int continuedEventCode)
    {
        if (state.StateTicks < StageOpenTicks)
            return;

        state.StateTicks = 0;
        effects.PublishStateChange(config.Index2 < completedStage ? deniedEventCode : continuedEventCode);
    }

    private static void AdvanceTerminal(Zone175MissionState state, IZone175MissionEffects effects)
    {
        if (state.StateTicks < WaveTimeoutTicks)
            return;

        state.StateTicks = 0;
        effects.ForceDisconnectAll();
        effects.Notify(Zone175MissionEvent.TerminalKickReset, 0, 0);
        effects.PublishStateChange(110);
    }

    private static Zone175MissionPhase ToPhase(int sharedState, int idleBattleState)
    {
        if (sharedState == 0)
            return idleBattleState == 0 ? Zone175MissionPhase.Idle : Zone175MissionPhase.PreOpen;

        return sharedState switch
        {
            5 or 9 or 13 or 17 or 21 => Zone175MissionPhase.WaveCombat,
            23 => Zone175MissionPhase.Terminal,
            _ => Zone175MissionPhase.WaveBossSummon
        };
    }
}
