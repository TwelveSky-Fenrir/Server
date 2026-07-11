using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Single source of truth for the level-up "bonus item" milestone system (legacy <c>LevelBonus</c> arm step
///     + tribe-work sort-8 deferred claim). Split into two halves that MUST stay in lockstep:
///     <list type="bullet">
///         <item>
///             the ARM half (<see cref="IsArmableMilestone" /> / <see cref="ResolveHighestMilestoneCrossed" />)
///             decides which reached level stamps <c>PlayerRuntimeState.BonusItemValue</c>/<c>BonusItemLevel</c>;
///         </item>
///         <item>
///             the CLAIM half (<see cref="TryResolveClaimDrops" />) decides the item set granted when the
///             player later redeems that stored level.
///         </item>
///     </list>
///     A level is armable here <b>only</b> when its claim item set is known, so a player can never end up holding
///     an armed milestone that the claim path would reject with a disconnect (the legacy's own
///     "unrecognized stored level -&gt; disconnect" rule, S04_MyWork02.cpp:11233-11326). Everything is pure and
///     allocation-conscious: the arm predicate is a <see cref="FrozenSet{T}" /> lookup and the crossed-milestone
///     scan is over a small static array with no LINQ.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:5109-5132 (the live <c>LNW33</c> milestone-arming switch --
///     qualifying reached levels set the bonus flag + level and send the subtype-107 avatar-change notification;
///     the inner <c>#ifndef LNW33</c> immediate-drop variant at :5084-5106 and the second table-driven
///     definition at :5135-5158 are both dead in the shipped ReleaseEU33 build).
///     Server/Header/Protocol/DEFINE.h:452-483 (<c>LV_M2</c>=114 .. <c>LV_M33</c>=145).
///     Server/ts25zone/S04_MyWork02.cpp:11233-11326 (the tribe-work sort-8 deferred-claim item drops + the M33
///     previous-tribe-specific item stamped with enchant value 20).
///     Only levels 45/105/145's item ids are directly cited by this system's C19 behavior contract; 65/85's ids
///     (99701/99702) are inherited verbatim from the already-shipped Fenrir implementation
///     (<c>TribeActionService.ClaimLevelBonusAsync</c>) and are NOT re-invented here. The M-tier levels
///     114/120/126/132/138/144 (<see cref="DeferredMilestoneLevels" />) are deliberately NOT armable: their claim
///     item ids are unrecoverable from the contract ("Several M-tier cases additionally grant one of item 1458",
///     no per-level table), and arming a level whose claim can't be honored would strand the player.
/// </remarks>
public static class LevelMilestoneBonus
{
    // Named M-tier milestone levels (DEFINE.h:452-483). All 11 legacy milestone levels are declared for
    // reference; only the ones with a known claim table below are actually armable (see ArmableMilestoneLevels).
    public const int LvM2 = 114;
    public const int LvM8 = 120;
    public const int LvM14 = 126;
    public const int LvM20 = 132;
    public const int LvM26 = 138;
    public const int LvM32 = 144;
    public const int LvM33 = 145;

    /// <summary>
    ///     The milestone levels this system can both arm and honor a claim for. Kept identical to the set
    ///     <see cref="TryResolveClaimDrops" /> resolves, so arm and claim never disagree.
    /// </summary>
    public static readonly FrozenSet<int> ArmableMilestoneLevels =
        new[] { 45, 65, 85, 105, LvM33 }.ToFrozenSet();

    /// <summary>
    ///     Legacy milestone levels whose claim item set is not recoverable from the C19 contract and are
    ///     therefore intentionally NOT armed today (arming them would produce a claim that disconnects the
    ///     player). Declared for discoverability; wire these in once a legacy finding supplies their item ids.
    /// </summary>
    public static readonly FrozenSet<int> DeferredMilestoneLevels =
        new[] { LvM2, LvM8, LvM14, LvM20, LvM26, LvM32 }.ToFrozenSet();

    // Ascending, for ResolveHighestMilestoneCrossed's allocation-free scan. Mirrors ArmableMilestoneLevels.
    private static readonly int[] ArmableMilestoneLevelsAscending = [45, 65, 85, 105, LvM33];

    /// <summary>True when reaching <paramref name="level" /> arms a claimable milestone bonus.</summary>
    public static bool IsArmableMilestone(int level)
    {
        return ArmableMilestoneLevels.Contains(level);
    }

    /// <summary>
    ///     The highest armable milestone level strictly above <paramref name="previousLevel" /> and at or below
    ///     <paramref name="newLevel" />, or 0 when none was crossed. This is the value the arm step should store:
    ///     a single experience gain can jump several levels at once (<c>LevelProgressionCalculator.ResolveLevelUp</c>
    ///     resolves the whole jump in one step), and the legacy arms per level in ascending order, so the highest
    ///     milestone crossed is the one that survives -- matching the contract's "single field, unconditionally
    ///     overwritten, most recent milestone wins" edge case.
    /// </summary>
    public static int ResolveHighestMilestoneCrossed(int previousLevel, int newLevel)
    {
        var highest = 0;
        foreach (var milestone in ArmableMilestoneLevelsAscending)
            if (milestone > previousLevel && milestone <= newLevel)
                highest = milestone;

        return highest;
    }

    /// <summary>
    ///     The deferred-claim item set for a stored <paramref name="bonusItemLevel" />. Returns false for any
    ///     level not in <see cref="ArmableMilestoneLevels" /> (including a stored 0 or a negative) -- the caller
    ///     must treat that exactly as the legacy does an unrecognized stored level: disconnect, grant nothing.
    /// </summary>
    /// <param name="previousTribe">
    ///     0-2, selects the M33 previous-tribe-specific item (83809/83857/83906). An out-of-range value simply
    ///     omits that one item, mirroring the legacy switch's own no-match fall-through.
    /// </param>
    public static bool TryResolveClaimDrops(int bonusItemLevel, byte previousTribe,
        out ImmutableArray<TribeGroundItemDrop> drops)
    {
        switch (bonusItemLevel)
        {
            case 45:
                drops = [new TribeGroundItemDrop(99700, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 65:
                // 99701 inherited from the shipped implementation; not a C19-contract-cited id (see remarks).
                drops = [new TribeGroundItemDrop(99701, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 85:
                // 99702 inherited from the shipped implementation; not a C19-contract-cited id (see remarks).
                drops = [new TribeGroundItemDrop(99702, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 105:
                drops = [new TribeGroundItemDrop(845, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case LvM33:
                drops = BuildM33ClaimDrops(previousTribe);
                return true;
            default:
                drops = default;
                return false;
        }
    }

    private static ImmutableArray<TribeGroundItemDrop> BuildM33ClaimDrops(byte previousTribe)
    {
        var tribeItemId = previousTribe switch
        {
            0 => 83809,
            1 => 83857,
            2 => 83906,
            _ => 0
        };

        var builder = ImmutableArray.CreateBuilder<TribeGroundItemDrop>(tribeItemId == 0 ? 4 : 5);
        builder.Add(new TribeGroundItemDrop(851, 1));
        builder.Add(new TribeGroundItemDrop(1022, 10));
        builder.Add(new TribeGroundItemDrop(1023, 10));
        builder.Add(new TribeGroundItemDrop(1019, 10));

        // Contract: ONE previous-tribe item "stamped with enchant value 20" -- quantity 1, not 20 copies (the
        // already-shipped TribeActionService reads the enchant literal as a quantity, a latent bug this table
        // corrects). The enchant-20 stamp itself is NOT expressible: TribeGroundItemDrop carries no enchant
        // field (ground-item drop plumbing is owned elsewhere). Flagged as a follow-up; see this task's notes.
        if (tribeItemId != 0)
            builder.Add(new TribeGroundItemDrop(tribeItemId, 1));

        return builder.ToImmutable();
    }
}
