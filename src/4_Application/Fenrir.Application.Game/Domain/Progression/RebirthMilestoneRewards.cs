using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Rebirth-generation milestone rewards, evaluated on the character's rebirth counter
///     <em>after</em> it has just been incremented (legacy <c>MyWork::MaxRebirth</c>,
///     <c>Server/ts25zone/S04_MyWork05.cpp:4828-4865</c>).
/// </summary>
/// <remarks>
///     Called by the rebirth path right after the counter is bumped. Only the exact post-increment
///     values 6 and 12 do anything -- every other value (1-5, 7-11) is a complete no-op (strict
///     equality, not a threshold: because the counter rises by exactly 1 per rebirth and is capped at
///     12, each character crosses 6 then 12 at most once each).
///     <list type="bullet">
///         <item>
///             Generation exactly 6: drop one milestone reward item on the ground at the character's
///             position (id chosen by previous tribe: 0->13553, 1->33553, 2->53553), no notice
///             (<c>S04_MyWork05.cpp:4832-4841</c>; the "6 rebirth" notice is dead-commented,
///             <c>:4842-4848</c>).
///         </item>
///         <item>
///             Generation exactly 12: the same ground drop (ids 13554/33554/53554), then an
///             <em>unconditional</em> cluster notice "&lt;name&gt; reached 12 rebirth!" that fires
///             regardless of whether the drop succeeded (<c>S04_MyWork05.cpp:4851-4863</c>). Drop
///             precedes notice.
///         </item>
///     </list>
///     A previous tribe outside {0,1,2} leaves the reward id at 0; legacy's item lookup for id 0 fails
///     so no item drops, but at generation 12 the notice still fires (the notice does not depend on the
///     drop). In practice previous tribe is always 0/1/2 from character creation, but the legacy code
///     does not guard against it, so neither do we.
///     The ground drop is created with quantity 0 and value 0, tagged with the character as owner, via
///     the "level bonus to world" drop source (<c>DP_LV_TO_WD</c> = 40, <c>DEFINE.h:526</c>). The item
///     ids appear elsewhere in Fenrir only as equipment-validation guards, never as reward items.
/// </remarks>
public static class RebirthMilestoneRewards
{
    /// <summary>First rewarded rebirth generation (ground drop only, no notice).</summary>
    public const int FirstMilestoneGeneration = 6;

    /// <summary>Second rewarded rebirth generation (ground drop plus cluster notice).</summary>
    public const int SecondMilestoneGeneration = 12;

    /// <summary>
    ///     Legacy <c>DP_LV_TO_WD</c> (<c>Server/Header/Protocol/DEFINE.h:526</c> = 40): "from level
    ///     bonus to world" ground-drop source category. Neither the monster-kill (1) nor manual (2)
    ///     party-share branch applies, so the item is owner-claimable by its tagged character only.
    /// </summary>
    public const int LevelToWorldDropSort = 40;

    /// <summary>Legacy creates the milestone ground item with quantity 0.</summary>
    private const int RewardQuantity = 0;

    /// <summary>
    ///     Resolves the milestone reward for a just-incremented rebirth counter. Returns an empty drop
    ///     set and no notice for any non-milestone generation, and an empty drop set (but still the
    ///     notice, at generation 12) when the previous tribe yields no reward id.
    /// </summary>
    public static RebirthMilestoneReward Resolve(int postIncrementRebirthCount, byte previousTribe)
    {
        return postIncrementRebirthCount switch
        {
            FirstMilestoneGeneration => new RebirthMilestoneReward(
                BuildRewardDrop(RewardItemIdAtSixthRebirth(previousTribe)), ClusterNotice: false),
            SecondMilestoneGeneration => new RebirthMilestoneReward(
                BuildRewardDrop(RewardItemIdAtTwelfthRebirth(previousTribe)), ClusterNotice: true),
            _ => new RebirthMilestoneReward(ImmutableArray<TribeGroundItemDrop>.Empty, ClusterNotice: false)
        };
    }

    /// <summary>The exact cluster-notice text for the twelfth rebirth (legacy <c>"%s reached 12 rebirth!"</c>).</summary>
    public static string FormatTwelfthRebirthNotice(string characterName)
    {
        return $"{characterName} reached 12 rebirth!";
    }

    private static int RewardItemIdAtSixthRebirth(byte previousTribe)
    {
        return previousTribe switch
        {
            0 => 13553,
            1 => 33553,
            2 => 53553,
            _ => 0
        };
    }

    private static int RewardItemIdAtTwelfthRebirth(byte previousTribe)
    {
        return previousTribe switch
        {
            0 => 13554,
            1 => 33554,
            2 => 53554,
            _ => 0
        };
    }

    private static ImmutableArray<TribeGroundItemDrop> BuildRewardDrop(int rewardItemId)
    {
        return rewardItemId == 0
            ? ImmutableArray<TribeGroundItemDrop>.Empty
            : [new TribeGroundItemDrop(rewardItemId, RewardQuantity, LevelToWorldDropSort)];
    }
}

/// <summary>
///     Outcome of <see cref="RebirthMilestoneRewards.Resolve" />: the (possibly empty) ground drops to
///     spawn at the character's position, and whether the unconditional twelfth-rebirth cluster notice
///     fires. The drop must be applied before the notice is sent.
/// </summary>
public readonly record struct RebirthMilestoneReward(
    ImmutableArray<TribeGroundItemDrop> Drops,
    bool ClusterNotice);
