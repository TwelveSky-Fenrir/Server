using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Forge;

/// <summary>
///     Pure resolver for the refine/smelt success roll (would back CZ_SMELT_ITEM_SEND / "op102"). No I/O, no
///     Zone dependency.
/// </summary>
/// <remarks>
///     IMPORTANT -- this is a PRODUCT-DECISION new-feature scaffold, NOT legacy parity. The smelt opcode is DEAD
///     in the shipped ReleaseEU33 build: its registration is gated on <c>USE_REFINE</c>
///     (Server/ts25zone/S04_MyWork01.cpp:111-113), which is defined only in the never-compiled <c>#else</c> of
///     <c>#ifdef M33</c> and then <c>#undef</c>'d under LNW33 (Server/Header/Protocol/DEFINE.h:34,108). So no
///     client can trigger refine today, and nothing here should be wired as a faithful reproduction of a live
///     legacy path. It exists so that IF refine is revived as a deliberate Fenrir feature, the one piece the
///     legacy source does still compile -- the success-rate formula -- is already modeled and cited.
///     <para>
///         The ONLY thing the C12 contract cites is the rate: with <c>USE_NEW_REFINE_RATE</c> also dead
///         (DEFINE.h:35), the live-compiled body of <c>GetRefineRate</c> is the linear form
///         <c>72 - 2 * (currentRefine + addedLevel)</c> (Server/Header/function.h:1636-1639). The tabled
///         variant (function.h:1602-1635) is dead code. <see cref="MaxRefine" /> (25,
///         <c>MAX_REFINE_ITEM_NUM</c>, DEFINE.h:616) is cited too.
///     </para>
///     <para>
///         Everything else about refine -- the money/material cost, which materials are accepted, the
///         <c>ProtectForRefine</c> (Preserve Charm) charge behavior on failure, and the exact item-state change
///         on success -- is NOT in any contract handed to this workstream (it is dead code, so no behavior
///         contract was produced for it). This resolver therefore models only the cited rate plus a plain
///         success/fail roll and a cap-clamped refine increment. On failure it makes NO state change; a
///         <c>ProtectForRefine</c>-charge interaction is deliberately left unmodeled rather than guessed. Any
///         revival must first obtain a real behavior contract for those pieces.
///     </para>
/// </remarks>
public static class RefineResolver
{
    public enum RefineOutcome
    {
        Rejected,
        Success,
        Failed
    }

    /// <summary>Refine hard cap (MAX_REFINE_ITEM_NUM, Server/Header/Protocol/DEFINE.h:616).</summary>
    public const int MaxRefine = 25;

    /// <summary>
    ///     Live-compiled linear success rate: <c>72 - 2 * (currentRefine + addedLevel)</c>, floored at 0 and
    ///     ceilinged at 100 (Server/Header/function.h:1636-1639). Percentage points for a 0..99 roll.
    /// </summary>
    public static int RefineRate(int currentRefine, int addedLevel)
    {
        var rate = 72 - 2 * (currentRefine + addedLevel);
        return Math.Clamp(rate, 0, 100);
    }

    /// <summary>
    ///     Consumes exactly one draw from <paramref name="random" />. Rejects a non-positive
    ///     <paramref name="addedLevel" /> or a target already at (or past) <see cref="MaxRefine" />. On success
    ///     the refine value advances by <paramref name="addedLevel" />, clamped to <see cref="MaxRefine" />; on
    ///     failure it is unchanged (see this type's remarks for why no protection/downgrade is modeled).
    /// </summary>
    public static RefineResult Resolve(int currentRefine, int addedLevel, IRandomSource random)
    {
        if (addedLevel <= 0 || currentRefine < 0 || currentRefine >= MaxRefine)
            return new RefineResult(RefineOutcome.Rejected, currentRefine, 0);

        var rate = RefineRate(currentRefine, addedLevel);

        if (random.NextInt32(100) < rate)
            return new RefineResult(RefineOutcome.Success, Math.Min(currentRefine + addedLevel, MaxRefine), rate);

        return new RefineResult(RefineOutcome.Failed, currentRefine, rate);
    }

    public readonly record struct RefineResult(RefineOutcome Outcome, int NewRefine, int Rate);
}
