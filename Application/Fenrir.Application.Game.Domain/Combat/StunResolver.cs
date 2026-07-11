using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

public enum StunRejectReason
{
    None,
    AttackerDead,
    AttackerProtected,
    DefenderDead,
    DefenderShopOpen,

    DefenderActionStateBlocksTargeting,

    DefenderProtected,

    NotAuthorized,

    AntiCheatEchoMismatch,

    NoStunSuccessValue
}

public readonly record struct StunAttemptOutcome(
    bool Rejected,
    StunRejectReason RejectReason,
    bool Success,
    int ResolvedDurationSeconds,
    bool IsTeamStunSkill)
{
    public static StunAttemptOutcome Reject(StunRejectReason reason)
    {
        return new StunAttemptOutcome(true, reason, false, 0, false);
    }

    public static StunAttemptOutcome Fail(bool isTeamStunSkill)
    {
        return new StunAttemptOutcome(false, StunRejectReason.None, false, 0, isTeamStunSkill);
    }
}

public readonly record struct StunAttemptRequest(
    CombatantSnapshot Attacker,
    CombatantSnapshot Defender,
    int DefenderActionSort,
    bool DefenderPshopOpen,
    bool ZoneAllowsEnemyTribeAttack,
    bool AttackerAndDefenderShareActiveDuel,
    bool AttackerVerifiesEchoedActionState,
    int UsedSkillId,
    int UsedSkillGradePoints,
    int AttackerAnimatingSkillNumber,
    int AttackerAnimatingGradePoints,
    SkillDefinition? UsedSkill,
    SkillDefinition? DefenderStunResistSkill,
    int DefenderStunResistGradePoints,
    bool DefenderHasStunImmunityBuff);

public static class StunResolver
{
    public const int TeamStunSkillId = 80;

    private const int NoActionYetSort = 0;

    private const int DeathPoseSort = 12;

    private const int TeamStunRollRange = 100;

    private const int OrdinaryStunRollRange = 500;

    private const int StunDefenseBuffBlockValue = 100;

    public static readonly ImmutableArray<int> StunResistSkillIds = [5, 24, 43];

    public static StunAttemptOutcome Resolve(StunAttemptRequest request, TimeSpan zoneClock, IRandomSource rng)
    {
        var attacker = request.Attacker;
        var defender = request.Defender;

        if (attacker.IsDead)
            return StunAttemptOutcome.Reject(StunRejectReason.AttackerDead);
        if (attacker.ZoneEntryAtZoneClock is { } attackerEntry &&
            zoneClock - attackerEntry < CombatResolver.ProtectDuration)
            return StunAttemptOutcome.Reject(StunRejectReason.AttackerProtected);

        if (defender.IsDead)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderDead);
        if (request.DefenderPshopOpen)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderShopOpen);
        if (request.DefenderActionSort is NoActionYetSort or DeathPoseSort)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderActionStateBlocksTargeting);
        if (defender.ZoneEntryAtZoneClock is { } defenderEntry &&
            zoneClock - defenderEntry < CombatResolver.ProtectDuration)
            return StunAttemptOutcome.Reject(StunRejectReason.DefenderProtected);

        var authorized = (request.ZoneAllowsEnemyTribeAttack && attacker.Tribe != defender.Tribe) ||
                         request.AttackerAndDefenderShareActiveDuel;
        if (!authorized)
            return StunAttemptOutcome.Reject(StunRejectReason.NotAuthorized);

        if (request.AttackerVerifiesEchoedActionState &&
            (request.UsedSkillId != request.AttackerAnimatingSkillNumber ||
             request.UsedSkillGradePoints != request.AttackerAnimatingGradePoints))
            return StunAttemptOutcome.Reject(StunRejectReason.AntiCheatEchoMismatch);

        if (request.UsedSkill is null)
            return StunAttemptOutcome.Reject(StunRejectReason.NoStunSuccessValue);

        var successValue =
            (int)SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.StunAttack);
        if (successValue < 1)
            return StunAttemptOutcome.Reject(StunRejectReason.NoStunSuccessValue);

        var isTeamStunSkill = request.UsedSkillId == TeamStunSkillId;

        int block;
        if (isTeamStunSkill)
            block = 0;
        else if (request.DefenderHasStunImmunityBuff)
            block = StunDefenseBuffBlockValue;
        else if (request.DefenderStunResistSkill is { } resistSkill)
            block = (int)SkillCatalog.ReturnSkillValue(resistSkill, request.DefenderStunResistGradePoints,
                SkillValueKind.StunDefense);
        else
            block = 0;

        var rollRange = isTeamStunSkill ? TeamStunRollRange : OrdinaryStunRollRange;
        var margin = successValue - block;
        var success = margin > 0 && rng.NextInt32(rollRange) < margin;
        if (!success)
            return StunAttemptOutcome.Fail(isTeamStunSkill);

        var durationSeconds =
            (int)SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.RunTime);
        if (durationSeconds < 1)
            return StunAttemptOutcome.Fail(isTeamStunSkill);

        return new StunAttemptOutcome(false, StunRejectReason.None, true, durationSeconds, isTeamStunSkill);
    }
}
