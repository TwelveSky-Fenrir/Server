using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

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
///     Each gate firing that computes a nonzero HP and/or MP amount pushes an
///     <see cref="AvatarStatUpdateResponse" /> to the sitting player's own connection only, one packet per
///     nonzero stat -- S07_MyGame04.cpp:481-489 (HP push) and :509-519 (MP push), both via the
///     single-connection overload confirmed by Server/ts25zone/S05_MyTransfer.cpp:519-542 and
///     Server/ts25zone/H05_MyTransfer.h:33-34. A stat whose recovery amount computes to zero (already at max,
///     or an unresolved skill/grade &lt; 1) sends nothing for that stat -- this is independent per stat, unlike
///     the dirty-tracker mark below which fires once for both stats together purely for write-behind batching.
/// </remarks>
public sealed class MeditationRegenSystem(WorldDataCache worldData, DirtyTracker<int> dirtyTracker)
    : ISimulationSystem
{
    private const int MeditationActionSort = 31;

    /// <summary>
    ///     S010CHARACTER_HP (Server/Header/Protocol/STRUCT.h:1525) -- same literal already used by
    ///     <c>Zone.CharacterHpStatSort</c> (Zone.PlayerLifecycle.cs), duplicated locally because this system
    ///     sits in a different class from the <c>Zone</c> partial-class family that owns that constant.
    /// </summary>
    private const int CharacterHpStatSort = 10;

    /// <summary>
    ///     S011CHARACTER_MP (Server/Header/Protocol/STRUCT.h:1526) -- same literal already used by
    ///     <c>Zone.CharacterMpStatSort</c> (Zone.CosmeticMirrors.cs), duplicated locally for the same reason
    ///     as <see cref="CharacterHpStatSort" />.
    /// </summary>
    private const int CharacterMpStatSort = 11;

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

            // Each stat's push is independent -- a stat whose amount computed to zero (already at max, or an
            // unresolved skill/grade < 1) neither changes nor sends a packet, matching the contract's "no
            // packet for a stat whose recovery computes to zero" rule.
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
