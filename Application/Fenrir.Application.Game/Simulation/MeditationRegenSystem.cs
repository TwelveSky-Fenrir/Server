using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Passive HP/MP regeneration WHILE MEDITATING (report 05 §7 point 3, verified
///     <c>AVATAR_OBJECT::Update</c>, <c>Server/ts25zone/S07_MyGame04.cpp:461-518</c>): <c>aAction.aSort == 31</c>
///     (sitting) is the ONLY passive-regen state in this codebase -- there is NO passive regen while standing
///     (report's own explicit callout: "Aucune régène passive debout"). Per legacy tick:
///     <c>
///         regen = MaxLife /
///         ReturnSkillValue(sitSkill, sitGradePoints, factor 2)
///     </c>
///     (mana: factor 3), floor 1, clamped to the
///     remaining capacity -- both driven by the CURRENT sit-skill riding on <c>PlayerRuntimeState.ActionSort</c>
///     (<see cref="PlayerRuntimeState.ActionSkillNumber" />/<c>ActionSkillGradeNum1/2</c>, set by
///     <c>Zone.HandleMove</c> from the same unified avatar-action wire every other action already uses).
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
