namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The two base-amount lookups <see cref="RegularWarRewardCalculator" /> needs but this cluster's behavior
///     contract only specifies as a citation + numeric range, not literal per-tier/per-level values:
///     <list type="bullet">
///         <item>
///             Money, keyed by rebirth tier 1-12 (tier 0 = zero): Server/ts25zone/S07_MyGame01.cpp:7108-7165
///             (<c>GetLevelBattleRewardInfo</c>) -- contract only states the range (10,000,000-20,000,000), not
///             each of the 12 rows.
///         </item>
///         <item>
///             Experience, keyed by plain character level: Server/ts25zone/S07_MyGame01.cpp:405-423
///             (<c>GetBattleExpReward</c>) -- no formula/table was captured in the contract at all.
///         </item>
///     </list>
///     Per the "no legacy parity from memory" rule, Fenrir does not fabricate either table here. This
///     abstraction lets every OTHER Regular War reward mechanic (CP bonus, hero points, leaderboard CP,
///     losing-side halving, participation, item-drop eligibility) be implemented and tested for real today,
///     with the exact production numbers dropped in later behind this same seam once a
///     legacy-behavior-translator contract supplies the literal 12-row table and the real
///     <c>GetBattleExpReward</c> formula. <see cref="UnavailableRegularWarRewardValueProvider" /> is the safe
///     default (always zero) wired in <c>Fenrir.Application.Game.Hosting</c> until then.
/// </summary>
public interface IRegularWarRewardValueProvider
{
    /// <summary><paramref name="rebirthTier" /> is <c>aLevel2</c> (0 = not yet reached, 1-12 = the post-cap ladder).</summary>
    public long GetMoneyReward(short rebirthTier);

    /// <summary><paramref name="level" /> is the character's plain <c>aLevel1</c>.</summary>
    public int GetExperienceReward(short level);
}

/// <summary>
///     Safe default: always zero. See <see cref="IRegularWarRewardValueProvider" />'s remarks for why the real
///     12-row money table and level-based XP formula are not guessed at here.
/// </summary>
public sealed class UnavailableRegularWarRewardValueProvider : IRegularWarRewardValueProvider
{
    public long GetMoneyReward(short rebirthTier)
    {
        return 0;
    }

    public int GetExperienceReward(short level)
    {
        return 0;
    }
}
