namespace Fenrir.Application.Game.Domain.Simulation;

public static class Zone175MissionCore
{

        public static void Advance(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, DateTimeOffset nowUtc, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        state.SubTick += legacyTicksElapsed;

        switch (state.Phase)
        {
            case Zone175MissionPhase.Idle:
                AdvanceIdle(state, effects, nowUtc);
                break;
            case Zone175MissionPhase.PreOpen:
                AdvancePreOpen(state, in config, effects, legacyTicksElapsed);
                break;
            case Zone175MissionPhase.WaveBossSummon:
                AdvanceBossSummon(state, effects);
                break;
            case Zone175MissionPhase.WaveCombat:
                AdvanceCombat(state, in config, effects, legacyTicksElapsed);
                break;
            case Zone175MissionPhase.Terminal:
                AdvanceTerminal(state, effects, legacyTicksElapsed);
                break;
        }
    }

        public static bool IsOpenMoment(DateTimeOffset nowUtc)
    {
        return nowUtc.DayOfWeek == DayOfWeek.Sunday && nowUtc.Hour == 21 && nowUtc.Minute == 0;
    }

    private static void AdvanceIdle(Zone175MissionState state, IZone175MissionEffects effects, DateTimeOffset nowUtc)
    {
        if (!IsOpenMoment(nowUtc))
            return;

        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        if (state.LastOpenedDateUtc == today)
            return;

        state.LastOpenedDateUtc = today;
        state.SubTick = 0;
        state.CurrentWave = 0;
        state.PreOpenRemaining = Zone175RewardTables.PreOpenCountStart;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
        state.Phase = Zone175MissionPhase.PreOpen;

        effects.Notify(Zone175MissionEvent.MissionOpen, 0, state.PreOpenRemaining);
    }

    private static void AdvancePreOpen(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, int legacyTicksElapsed)
    {
        state.PhaseAccumulatorTicks += legacyTicksElapsed;

        while (state.PreOpenRemaining > 0 &&
               state.PhaseAccumulatorTicks >= Zone175RewardTables.PreOpenCountdownCadenceTicks)
        {
            state.PhaseAccumulatorTicks -= Zone175RewardTables.PreOpenCountdownCadenceTicks;
            state.PreOpenRemaining--;
            effects.Notify(Zone175MissionEvent.PreOpenCountdown, 0, state.PreOpenRemaining);
        }

        if (state.PreOpenRemaining <= 0)
            BeginWave(state, in config, effects, 1);
    }

        private static void BeginWave(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, int wave)
    {
        _ = config;
        state.CurrentWave = wave;
        state.Phase = Zone175MissionPhase.WaveBossSummon;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
        effects.Notify(Zone175MissionEvent.WaveGateOpen, wave, 0);
    }

    private static void AdvanceBossSummon(Zone175MissionState state, IZone175MissionEffects effects)
    {
        effects.Notify(Zone175MissionEvent.WaveBossSummon, state.CurrentWave, 0);
        effects.SummonWaveBoss(state.CurrentWave);
        state.Phase = Zone175MissionPhase.WaveCombat;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
    }

    private static void AdvanceCombat(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, int legacyTicksElapsed)
    {
        state.PhaseAccumulatorTicks += legacyTicksElapsed;

        if (!effects.AnyQualifyingPlayerPresent())
        {
            AbortWave(state, effects, Zone175MissionEvent.EmptyAbort);
            return;
        }

        if (state.PhaseAccumulatorTicks >= Zone175RewardTables.WaveTimeoutLegacyTicks)
        {
            AbortWave(state, effects, Zone175MissionEvent.WaveTimeout);
            return;
        }

        if (effects.CountLivingWaveBosses(state.CurrentWave) == 0)
        {
            ClearWave(state, in config, effects);
            return;
        }

        state.TrickleAccumulatorTicks += legacyTicksElapsed;
        while (state.TrickleAccumulatorTicks >= Zone175RewardTables.TrickleCadenceSubTicks)
        {
            state.TrickleAccumulatorTicks -= Zone175RewardTables.TrickleCadenceSubTicks;
            effects.SummonTrickle(state.CurrentWave);
        }
    }

    private static void ClearWave(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects)
    {
        var clearedWave = state.CurrentWave;
        effects.RemoveMissionMonsters();
        effects.RewardQualifyingPlayers(clearedWave);
        effects.Notify(Zone175MissionEvent.WaveCleared, clearedWave, 0);

        if (clearedWave >= Zone175RewardTables.WaveCount)
        {
            effects.Notify(Zone175MissionEvent.MissionEnd, clearedWave, 0);
            EnterTerminal(state, effects);
            return;
        }

        if (Zone175RewardTables.CanAdvanceToNextWave(clearedWave, config.Index2))
        {
            BeginWave(state, in config, effects, clearedWave + 1);
        }
        else
        {
            effects.Notify(Zone175MissionEvent.DepthGateStop, clearedWave, 0);
            EnterTerminal(state, effects);
        }
    }

    private static void AbortWave(Zone175MissionState state, IZone175MissionEffects effects,
        Zone175MissionEvent reason)
    {
        effects.RemoveMissionMonsters();
        effects.Notify(reason, state.CurrentWave, 0);
        EnterTerminal(state, effects);
    }

    private static void EnterTerminal(Zone175MissionState state, IZone175MissionEffects effects)
    {
        state.Phase = Zone175MissionPhase.Terminal;
        state.PhaseAccumulatorTicks = 0;
        effects.Notify(Zone175MissionEvent.TerminalEnter, state.CurrentWave, 0);
    }

    private static void AdvanceTerminal(Zone175MissionState state, IZone175MissionEffects effects,
        int legacyTicksElapsed)
    {
        state.PhaseAccumulatorTicks += legacyTicksElapsed;
        if (state.PhaseAccumulatorTicks < Zone175RewardTables.TerminalHoldLegacyTicks)
            return;

        effects.ForceDisconnectAll();
        effects.Notify(Zone175MissionEvent.TerminalKickReset, state.CurrentWave, 0);

        state.Phase = Zone175MissionPhase.Idle;
        state.CurrentWave = 0;
        state.SubTick = 0;
        state.PreOpenRemaining = 0;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
    }
}
