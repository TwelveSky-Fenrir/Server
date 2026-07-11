using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Numeric literals for the two C15 kill-feed reward channels that are NOT the leaderboard/broadcast
///     mechanism itself: the FFA-335 per-kill War Point/Blood Point award (steps 6-8 of the source contract)
///     and the end-of-battle top-1/2/3 Contribution Point reward (steps 9-11).
/// </summary>
/// <remarks>
///     Kept deliberately separate from <see cref="PvpKillContributionPointCalculator" />'s own
///     <c>FfaOverrideFlatAmount</c>/<c>RegularWarOverrideFlatCpAmount</c> (+20 CP each): those model a
///     textually distinct legacy function (<c>FFA_ProcessKillReward</c>/<c>RegularWar_ProcessKillReward</c>,
///     S07_MyGame03.cpp:2402-2504) with its own 2-minute cooldown. This class's constants come from a
///     different cited range entirely (S07_MyGame02.cpp:56-171, the kill-feed routine's own FFA points path)
///     with its own 3-minute cooldown -- both are real, separately-cited legacy mechanisms that happen to
///     both fire on an FFA kill; see <see cref="World.Zone.RecordEnemyKillForFeed" />'s own remarks.
/// </remarks>
public static class KillFeedRewardConstants
{
    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:97-111,168.</summary>
    public const int FfaWarPointPerKill = 2;

    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:112-123,169 ; S07_MyGame03.cpp:2354-2360.</summary>
    public const int FfaBloodPointPerKill = 2;

    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:68-70 (FFA top CP constants).</summary>
    public const int FfaTop1ContributionPoints = 200;

    public const int FfaTop2ContributionPoints = 100;
    public const int FfaTop3ContributionPoints = 80;

    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:453-471 (non-FFA war-zone top CP constants).</summary>
    public const int NonFfaTop1ContributionPoints = 100;

    public const int NonFfaTop2ContributionPoints = 50;
    public const int NonFfaTop3ContributionPoints = 25;

    /// <summary>
    ///     Zone-267-type instances double every non-FFA top-CP amount (100/50/25 -&gt; 200/100/50). Réf. C++ :
    ///     Server/ts25zone/S07_MyGame02.cpp:453-471.
    /// </summary>
    public const int Zone267ContributionPointMultiplier = 2;

    /// <summary>
    ///     Per-ordered-pair (attacker, victim) anti-farm window for the FFA War Point/Blood Point award only --
    ///     independent of every other cooldown table in this codebase (<see cref="KillCooldownTracker" />'s own
    ///     C05 10-minute gate, and <see cref="PvpKillContributionPointCalculator.FlatOverrideCooldown" />'s own
    ///     2-minute gate). Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:56-61 (three times the minute-tick helper);
    ///     Server/Header/datetime.h:29,33,46 (minute tick = 60000 ms).
    /// </summary>
    public static readonly TimeSpan FfaAntiFarmCooldown = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     FFA participant one-time item bundle -- FOUR PLACEHOLDER item ids (white-feather, mount-box,
    ///     pet-box, cape-box), NOT recoverable production values. The source behavior contract's own Open
    ///     Question 1 states these four literals (695/601/602/2249) appear in the legacy source each carrying
    ///     an explicit placeholder TODO comment, meaning the legacy author's own intended production ids were
    ///     never filled in -- treat these as "some item is granted in each of these four slots," never as
    ///     authoritative ids. Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:74-77.
    /// </summary>
    public static readonly ImmutableArray<int> FfaParticipantBundleItemIdsPlaceholder = [695, 601, 602, 2249];
}
