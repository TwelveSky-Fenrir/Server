namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Port of <c>MyUser::SetUserBonus2</c>'s <c>mSupportSkillTimeUpRatio</c> computation
///     (Server/ts25zone/S03_MyUser.cpp:524-541): the self-buff skill duration multiplier consumed by
///     <see cref="SkillCastResolver.TryCast" />. Starts at 1x, doubles while a Buff-Duration-Extension
///     consumable (legacy <c>aBuffX2Time</c>) is active, and doubles again while Premium status (legacy
///     <c>aPremium</c>) is unexpired -- the two doublings compound (both active -&gt; 4x), never add. Pure
///     and stateless by design: the caller (<see cref="World.PlayerRuntimeState.RecomputeSupportSkillTimeUpRatio" />)
///     owns caching the result and deciding when to call this again; this type never reads a clock or any
///     session field itself.
/// </summary>
public static class SupportSkillTimeUpRatioCalculator
{
    public static int Compute(bool buffDurationExtensionActive, bool premiumActive)
    {
        var ratio = 1;

        if (buffDurationExtensionActive)
            ratio *= 2;

        if (premiumActive)
            ratio *= 2;

        return ratio;
    }
}
