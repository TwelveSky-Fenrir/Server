namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Pure resolver for CZ_CONTINUE_SKILL_USE_SEND (op95) Sort=1 (start channeling) and Sort=2 (per-tick
///     apply). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Sort=2's real per-tick buff application (<c>ProcessForCreateEffectValue</c>, itself needing
///     <c>MyFactor::GetBonusSkillValue</c>) is out of scope here -- it needs its own follow-up investigation
///     into the buff/effect-value system. Its wire behaviour is reproduced exactly regardless (no reply, no
///     disconnect, matching the legacy's own silent per-tick handling), so this gap is not wire-observable.
/// </remarks>
public static class AutoBuffActivationResolver
{
    public enum ResultKind
    {
        /// <summary>Legacy silently returns with no reply and no state change.</summary>
        NoReply,

        /// <summary>A real Quit() condition -- the caller must disconnect.</summary>
        Disconnect,
        Activate,
        Tick
    }

    /// <summary>aAction.aSort after a successful Sort=1 activation.</summary>
    public const int ChannelingActionSort = 41;

    public static Result Resolve(int sort, in Context ctx, int today)
    {
        switch (sort)
        {
            case 1:
                if (ctx.AutoBuffTime < today || ctx.ActionSort != 1)
                    return new Result(ResultKind.NoReply);

                // (int)(mana * 0.9f) is never greater than mana for mana >= 0, so this can never actually
                // trigger -- reproduced verbatim from the C++ source rather than dropped as dead code (D8).
                var reducedMana = (int)(ctx.Mana * 0.9f);
                return ctx.Mana < reducedMana
                    ? new Result(ResultKind.Disconnect)
                    : new Result(ResultKind.Activate, ctx.Mana - reducedMana);

            case 2:
                return new Result(ResultKind.Tick);

            default:
                return new Result(ResultKind.Disconnect);
        }
    }

    public readonly record struct Result(ResultKind Kind, int ManaAfterActivation = 0);

    public readonly record struct Context(int AutoBuffTime, int ActionSort, int Mana);
}
