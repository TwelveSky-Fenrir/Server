using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class MeditationRegenSystem(WorldDataCache worldData, DirtyTracker<int> dirtyTracker)
    : ISimulationSystem
{
    private const int MeditationActionSort = 31;

    private const int CharacterHpStatSort = 10;

    private const int CharacterMpStatSort = 11;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone || state.ActionSort != MeditationActionSort || state.IsDead ||
                legacyTicksElapsed <= 0)
                continue;

            var periodsElapsed = legacyTicksElapsed;

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

            if (life != state.Life)
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = CharacterHpStatSort, Value = life, Value2 = 0 });

            if (mana != state.Mana)
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = CharacterMpStatSort, Value = mana, Value2 = 0 });

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
