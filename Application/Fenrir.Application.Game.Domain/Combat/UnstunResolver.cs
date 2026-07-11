using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

public enum UnstunRejectReason
{
    None,
    CurerDead,
    TargetDead,
    TargetShopOpen,

        TargetNotStunned,

        TargetMovingZone,

        NotAuthorized,

        AntiCheatEchoMismatch,

        UnrecognizedSkill
}

public readonly record struct UnstunAttemptOutcome(bool Rejected, UnstunRejectReason RejectReason, bool Success)
{
    public static readonly UnstunAttemptOutcome Failed = new(false, UnstunRejectReason.None, false);
    public static readonly UnstunAttemptOutcome Succeeded = new(false, UnstunRejectReason.None, true);

    public static UnstunAttemptOutcome Reject(UnstunRejectReason reason)
    {
        return new UnstunAttemptOutcome(true, reason, false);
    }
}

public readonly record struct UnstunAttemptRequest(
    CombatantSnapshot Curer,
    CombatantSnapshot Target,
    bool TargetIsStunned,
    bool TargetPshopOpen,
    bool CurerVerifiesEchoedActionState,
    int UsedSkillId,
    int UsedSkillGradePoints,
    int CurerAnimatingSkillNumber,
    int CurerAnimatingGradePoints,
    SkillDefinition? UsedSkill);

public static class UnstunResolver
{

        public const int GuaranteedCureGradePoints = 20;

    private const int SuccessRollRange = 100;

        public static readonly ImmutableArray<int> StunResistSkillIds = [5, 24, 43];

    public static UnstunAttemptOutcome Resolve(UnstunAttemptRequest request, IRandomSource rng)
    {
        var curer = request.Curer;
        var target = request.Target;

        if (curer.IsDead)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.CurerDead);
        if (target.IsDead)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetDead);
        if (target.IsMovingZone)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetMovingZone);
        if (request.TargetPshopOpen)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetShopOpen);
        if (!request.TargetIsStunned)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.TargetNotStunned);

        if (curer.Tribe != target.Tribe)
            return UnstunAttemptOutcome.Reject(UnstunRejectReason.NotAuthorized);

        if (request.CurerVerifiesEchoedActionState)
        {
            if (!StunResistSkillIds.Contains(request.UsedSkillId))
                return UnstunAttemptOutcome.Reject(UnstunRejectReason.UnrecognizedSkill);
            if (request.UsedSkillId != request.CurerAnimatingSkillNumber ||
                request.UsedSkillGradePoints != request.CurerAnimatingGradePoints)
                return UnstunAttemptOutcome.Reject(UnstunRejectReason.AntiCheatEchoMismatch);
        }

        if (request.UsedSkillGradePoints >= GuaranteedCureGradePoints)
            return UnstunAttemptOutcome.Succeeded;

        if (request.UsedSkill is null)
            return UnstunAttemptOutcome.Failed;

        var doubledValue =
            (int)(SkillCatalog.ReturnSkillValue(request.UsedSkill, request.UsedSkillGradePoints,
                SkillValueKind.StunDefense) * 2);
        var success = doubledValue > 0 && rng.NextInt32(SuccessRollRange) < doubledValue;

        return success ? UnstunAttemptOutcome.Succeeded : UnstunAttemptOutcome.Failed;
    }
}
