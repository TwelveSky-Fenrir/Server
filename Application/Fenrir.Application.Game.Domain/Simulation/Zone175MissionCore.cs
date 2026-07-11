namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The pure Zone175 "Labyrinth" 5-wave mission state machine: a single
///     <see cref="Advance(Zone175MissionState,in Zone175InstanceConfig,IZone175MissionEffects,DateTimeOffset,int)" />
///     entry point that folds one batch of elapsed legacy ticks into <see cref="Zone175MissionState" />, calling
///     <see cref="IZone175MissionEffects" /> for every side effect. No allocation, no clock/world access of its
///     own -- everything comes in as arguments, which is what makes the whole lifecycle deterministically
///     unit-testable.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame01.cpp:8746-9311</c> (routine entry, sub-tick increment, state
///     dispatch, the idle open-gate, the five waves, the reward call, the terminal). The per-code numbered-state
///     advancement is center-driven in the legacy; Fenrir drives it locally (see <see cref="Zone175MissionPhase" />
///     remarks). The Sunday-21:00 open moment is evaluated in <b>UTC</b>, matching this codebase's
///     <c>GameDate</c>/<c>WorldStateService</c> convention -- flagged as an assumption should the legacy prove to
///     use local server time (<c>S07_MyGame01.cpp:8760-8780</c>).
/// </remarks>
public static class Zone175MissionCore
{
    /// <summary>
    ///     Advances the mission by <paramref name="legacyTicksElapsed" /> whole legacy ticks. A no-op when the
    ///     batch is non-positive (a clock hiccup) -- the same posture <see cref="SimulationTickAccumulator" />
    ///     takes.
    /// </summary>
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

    /// <summary>The Sunday-21:00-exactly open moment (UTC). No catch-up: a missed minute waits for the next occurrence.</summary>
    public static bool IsOpenMoment(DateTimeOffset nowUtc)
    {
        return nowUtc.DayOfWeek == DayOfWeek.Sunday && nowUtc.Hour == 21 && nowUtc.Minute == 0;
    }

    private static void AdvanceIdle(Zone175MissionState state, IZone175MissionEffects effects, DateTimeOffset nowUtc)
    {
        if (!IsOpenMoment(nowUtc))
            return;

        // Open at most once per matching minute: the gate is polled on every idle tick within the 21:00 minute.
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        if (state.LastOpenedDateUtc == today)
            return;

        // Side effect 1 (S07_MyGame01.cpp:8782-8812): reset sub-tick + battle sub-state, set countdown 10, write
        // the "mission start" log record (carried by the MissionOpen event), then begin the pre-open countdown.
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

        // Broadcast the decreasing remaining count once per one-minute cadence until it runs out. Catch-up loop
        // so a stalled host that accumulated several minutes' worth of ticks fires each intervening decrement.
        while (state.PreOpenRemaining > 0 &&
               state.PhaseAccumulatorTicks >= Zone175RewardTables.PreOpenCountdownCadenceTicks)
        {
            state.PhaseAccumulatorTicks -= Zone175RewardTables.PreOpenCountdownCadenceTicks;
            state.PreOpenRemaining--;
            effects.Notify(Zone175MissionEvent.PreOpenCountdown, 0, state.PreOpenRemaining);
        }

        if (state.PreOpenRemaining <= 0)
            // Countdown exhausted -> signal the center to begin -> wave 1. Fenrir drives this locally.
            BeginWave(state, in config, effects, 1);
    }

    /// <summary>Enters a wave's gate-open/boss-summon sequence: emits the gate-open event, arms boss-summon next tick.</summary>
    private static void BeginWave(Zone175MissionState state, in Zone175InstanceConfig config,
        IZone175MissionEffects effects, int wave)
    {
        _ = config; // reserved for a future per-wave, index-dependent variation; none is cited today.
        state.CurrentWave = wave;
        state.Phase = Zone175MissionPhase.WaveBossSummon;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
        effects.Notify(Zone175MissionEvent.WaveGateOpen, wave, 0);
    }

    private static void AdvanceBossSummon(Zone175MissionState state, IZone175MissionEffects effects)
    {
        // Boss-summon phase (S07_MyGame01.cpp:8842-8851 and the per-wave equivalents): summon this wave's boss
        // without a time limit, then hand off to the combat phase.
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

        // Order matches the legacy combat block (S07_MyGame01.cpp:8852-8910): presence scan / empty-abort first,
        // then the 60-minute timeout-abort, then the wave-boss clear check, then the 20-sub-tick trickle summon.
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
        // Wave clear (S07_MyGame01.cpp:8905-8909): remove all mission monsters, run the reward routine, announce.
        var clearedWave = state.CurrentWave;
        effects.RemoveMissionMonsters();
        effects.RewardQualifyingPlayers(clearedWave);
        effects.Notify(Zone175MissionEvent.WaveCleared, clearedWave, 0);

        if (clearedWave >= Zone175RewardTables.WaveCount)
        {
            // Fifth-wave clear additionally writes the "mission end" log record, then funnels into terminal.
            effects.Notify(Zone175MissionEvent.MissionEnd, clearedWave, 0);
            EnterTerminal(state, effects);
            return;
        }

        // Inter-wave depth gate: continue only if index2 permits, else end the mission short.
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
        // Empty/timeout abort (S07_MyGame01.cpp:8870-8885): monsters removed, mission routed to terminal.
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

        // Terminal (S07_MyGame01.cpp:9288-9308): after the 60-minute hold, force-disconnect everyone, signal the
        // mission back to idle, and reset the state to 0. Fires on every mission end (success and failure alike).
        effects.ForceDisconnectAll();
        effects.Notify(Zone175MissionEvent.TerminalKickReset, state.CurrentWave, 0);

        state.Phase = Zone175MissionPhase.Idle;
        state.CurrentWave = 0;
        state.SubTick = 0;
        state.PreOpenRemaining = 0;
        state.PhaseAccumulatorTicks = 0;
        state.TrickleAccumulatorTicks = 0;
        // LastOpenedDateUtc is deliberately left set so the mission does not re-open again the same Sunday.
    }
}
