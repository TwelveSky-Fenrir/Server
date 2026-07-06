using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     The personal 1000-kill CP milestone inside <c>ProcessAttack03</c>'s post-kill CP section
///     (<c>Server/ts25zone/S07_MyGame02.cpp:2752-2789</c>): every level-appropriate monster kill counts toward
///     a per-character counter (legacy <c>aKillMonsterNum2</c>, see
///     <see cref="World.PlayerRuntimeState.TowerCpMilestoneCounter" />);
///     the 1000th kill resets the counter to 0 and grants a CP reward -- a milestone reward, not a per-kill one.
/// </summary>
/// <remarks>
///     Eligibility uses <c>ReturnFixedLevel(attacker aLevel1+aLevel2)</c> minus the defender's real monster
///     level, strictly under 10 -- a different level basis than
///     <see cref="ExperienceFormulas.ComputeMonsterKillExperience" />'s own gap check, which only feeds
///     <c>aLevel1</c> (not <c>aLevel1+aLevel2</c>) through the same <see cref="ExperienceFormulas.ReturnFixedLevel" />.
///     Reproduced exactly as cited, not reconciled with the XP formula's own basis.
/// </remarks>
public static class TowerCpForPvmMilestone
{
    public const int KillsRequired = 1000;

    /// <summary>
    ///     Legacy base <c>killcp</c> before any tower bonus. The scroll/premium bumps described alongside this
    ///     milestone in the legacy source (a "double-kill" scroll charge bumping the base to 4, +2 more for
    ///     active premium) have no Fenrir equivalent yet -- same "feature absent" posture as
    ///     <see cref="ExperienceFormulas" />'s own remarks -- so only the scroll/premium-less base of 1 is
    ///     reproduced here.
    /// </summary>
    public const int BaseKillCp = 1;

    /// <summary>
    ///     Advances the milestone counter for one kill. A kill outside the level-appropriate window leaves the
    ///     counter untouched (does not count toward the milestone at all, matching legacy's own gate). Reaching
    ///     exactly <see cref="KillsRequired" /> resets the counter to 0 and reports the milestone as reached.
    /// </summary>
    public static KillRegistration RegisterKill(int counterBeforeThisKill, int attackerLevel1, int attackerLevel2,
        int monsterRealLevel)
    {
        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(attackerLevel1 + attackerLevel2);
        if (fixedLevel - monsterRealLevel >= 10)
            return new KillRegistration(counterBeforeThisKill, false);

        var next = counterBeforeThisKill + 1;
        return next >= KillsRequired
            ? new KillRegistration(0, true)
            : new KillRegistration(next, false);
    }

    /// <summary>Total CP awarded at the milestone: <see cref="BaseKillCp" /> plus the flat CP-for-PvM tower bonus.</summary>
    public static int ComputeReward(int cpForPvmTowerBonus)
    {
        return BaseKillCp + Math.Max(0, cpForPvmTowerBonus);
    }

    public readonly record struct KillRegistration(int UpdatedCounter, bool MilestoneReached);
}
