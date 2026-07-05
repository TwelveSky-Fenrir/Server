using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Passive HP/MP regen while meditating (AVATAR_OBJECT::Update, S07_MyGame04.cpp:461-518): aSort == 31
///     (sitting) is the only passive-regen state -- there is no passive regen while standing. Per legacy tick,
///     regen = MaxLife / ReturnSkillValue(sitSkill, gradePoints, factor 2) (mana: factor 3), floor 1.
/// </summary>
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

            if (!worldData.SkillsById.TryGetValue(state.ActionSkillNumber, out var skill))
                continue;

            var gradePoints = state.ActionSkillGradeNum1 + state.ActionSkillGradeNum2;
            var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
            var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

            var life = RegenerateOne(skill, gradePoints, legacyTicksElapsed, maxLife, state.Life,
                SkillValueKind.RecoverInfo1);
            var mana = RegenerateOne(skill, gradePoints, legacyTicksElapsed, maxMana, state.Mana,
                SkillValueKind.RecoverInfo2);

            if (life == state.Life && mana == state.Mana)
                continue;

            state.Life = life;
            state.Mana = mana;
            dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Vitals);
        }
    }

    private static int RegenerateOne(SkillDefinition skill, int gradePoints, int legacyTicksElapsed, int max,
        int current, SkillValueKind divisorKind)
    {
        if (current >= max)
            return current;

        var divisor = SkillCatalog.ReturnSkillValue(skill, gradePoints, divisorKind);
        if (divisor <= 0f)
            return current;

        var perTick = (int)(max / divisor);
        if (perTick < 1)
            perTick = 1;

        var total = perTick * legacyTicksElapsed;
        if (current + total > max)
            total = max - current;

        return current + total;
    }
}
