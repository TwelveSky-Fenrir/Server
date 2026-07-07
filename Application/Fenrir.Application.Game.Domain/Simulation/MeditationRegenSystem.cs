using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Passive HP/MP regen while meditating (AVATAR_OBJECT::Update, S07_MyGame04.cpp:461-518): aSort == 31
///     (sitting) is the only passive-regen state -- there is no passive regen while standing. Per gate firing,
///     regen = MaxLife / ReturnSkillValue(sitSkill, gradePoints, factor 2) (mana: factor 3), floor 1.
/// </summary>
/// <remarks>
///     Gated to <see cref="SimulationClock.MeditationRegenLegacyTicks" /> (2 legacy ticks, ~1 s) via
///     <see cref="PlayerRuntimeState.MeditationRegenAccumulatorTicks" /> -- the exact same shared
///     <c>mTickCountFor01Second == 2</c> strict-equality gate that <see cref="StunCountdownSystem" /> consumes
///     from the same C++ scope (S07_MyGame04.cpp:378-380, shared closing brace at :825). <see cref="Simulate" />
///     runs once per zone tick (potentially every ~50 ms at 20 Hz) with its own <c>legacyTicksElapsed</c>
///     parameter usually 0 and occasionally 1 at a legacy-tick (500 ms) boundary -- without this
///     per-character accumulator, the full ~1-second regen amount was applied on every one of those 500 ms
///     legacy ticks instead of once every two, i.e. twice the legacy rate (critical-severity fix).
///     A burst greater than 1 legacy tick in one call (host stall) fully catches up via integer division within
///     the same call, the same translation of "a burst must catch up by the full amount"
///     (<see cref="ISimulationSystem" />'s own contract) already chosen by <see cref="StunCountdownSystem" /> for
///     this identical gate.
/// </remarks>
public sealed class MeditationRegenSystem(WorldDataCache worldData, DirtyTracker<int> dirtyTracker)
    : ISimulationSystem
{
    private const int MeditationActionSort = 31;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.ActionSort != MeditationActionSort || state.IsDead)
                continue;

            state.MeditationRegenAccumulatorTicks += legacyTicksElapsed;
            var periodsElapsed = state.MeditationRegenAccumulatorTicks / SimulationClock.MeditationRegenLegacyTicks;
            if (periodsElapsed <= 0)
                continue;

            state.MeditationRegenAccumulatorTicks -= periodsElapsed * SimulationClock.MeditationRegenLegacyTicks;

            if (!worldData.SkillsById.TryGetValue(state.ActionSkillNumber, out var skill))
                continue;

            var gradePoints = state.ActionSkillGradeNum1 + state.ActionSkillGradeNum2;
            var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
            var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

            var life = RegenerateOne(skill, gradePoints, periodsElapsed, maxLife, state.Life,
                SkillValueKind.RecoverInfo1);
            var mana = RegenerateOne(skill, gradePoints, periodsElapsed, maxMana, state.Mana,
                SkillValueKind.RecoverInfo2);

            if (life == state.Life && mana == state.Mana)
                continue;

            state.Life = life;
            state.Mana = mana;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }
    }

    private static int RegenerateOne(SkillDefinition skill, int gradePoints, int periodsElapsed, int max,
        int current, SkillValueKind divisorKind)
    {
        if (current >= max)
            return current;

        var divisor = SkillCatalog.ReturnSkillValue(skill, gradePoints, divisorKind);
        if (divisor <= 0f)
            return current;

        var perPeriod = (int)(max / divisor);
        if (perPeriod < 1)
            perPeriod = 1;

        var total = perPeriod * periodsElapsed;
        if (current + total > max)
            total = max - current;

        return current + total;
    }
}
