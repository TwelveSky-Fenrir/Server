using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for item 76543 (Limited Costume Chest) -- workstream
///     C10-remaining-boxes. Unlike <see cref="OverEnchantBox8113RewardTable" />, this box does NOT fit any
///     existing <see cref="BoxRewardKind" />: its reward id is selected DETERMINISTICALLY by the opening
///     character's own stored previous-tribe code (no roll at all for the id itself), and it stamps a second,
///     independently-rolled value ("enchant grade", 30-60 inclusive) onto the granted item -- neither a tribe
///     input nor a secondary stamped value has any home in <see cref="BoxRewardSpec" />/
///     <see cref="Inventory.UseItems.Boxes.LootBoxOpenResolver" />
///     today. This file therefore supplies pure DATA plus a self-contained, fully-tested roll primitive
///     (<see cref="Roll" />) rather than a <see cref="BoxRewardSpec" />; wiring it into the shared open mechanism
///     requires extending that mechanism's shape first -- see this workstream's wiring manifest for the precise
///     required diff, deliberately NOT made here (this file only adds new types, per the "new files only" rule
///     for this pass).
/// </summary>
/// <remarks>
///     Réf. C++ (C10-remaining-boxes contract) : Server/ts25zone/S04_MyWork03.cpp:1732-1750 (bulk-path
///     <c>OpenSingleBoxNoStellar</c> for 76543: tribe map, 31-outcome enchant-grade roll, 3-day rental, and an
///     explicit early-return rejection arm for an unrecognized tribe) and :8023-8045 (single-path production case
///     block -- confirmed by the contract's own direct re-read to share the identical tribe map/roll/rental
///     values but to LACK that explicit rejection arm: an unrecognized tribe there falls through with the
///     reward-id variable left zero-initialized, still consumes an enchant-grade draw, and only fails later at the
///     shared inventory-mutation routine's catalog-lookup gate). The two paths converge on the same
///     client-visible outcome (reject, nothing granted) for an unrecognized tribe; they merely disagree on
///     whether a random draw is spent along the way. This codebase does not attempt bit-for-bit legacy RNG-draw-
///     order parity anywhere in the loot-box family (see <see cref="StellarBox8112RewardTable" />'s own remarks:
///     "not reproduced bit-for-bit"), so <see cref="Roll" /> below deliberately validates the tribe FIRST (the
///     bulk-path's own shape) rather than always spending a draw -- a documented simplification, not a legacy
///     citation gap.
///     <para>
///         Reward ids: ND(0)-&gt;76524, RS(1)-&gt;76525, GT(2)-&gt;76526 (:1737-1745 / :8029-8040). Enchant-grade
///         roll: uniform over 31 equally likely outcomes spanning 30 to 60 inclusive (:1747 / :8041). Rental:
///         a 3-day-later expiry is computed and applied unconditionally on success, since all three possible
///         reward ids fall inside the recognized rentable-id range (Server/Header/function.h:2216-2264,
///         <c>IsRentItem</c>, range 76500-76540 covers 76524-76526).
///     </para>
///     <para>
///         <b>Unresolved, not invented (open question):</b> the contract states the enchant-grade roll is
///         "written into the granted item's stored numeric 'value' field exactly as rolled, byte for byte"
///         (Server/Header/function.h:345-353, <c>ChangeISValue</c>, starting from a zero base) -- legacy packs
///         this as a single word, whereas Fenrir's own <see cref="Inventory.ItemStack" /> decomposes the
///         equivalent state into four separate byte columns (Enchant/Combine/Refine/Socket, see
///         <see cref="ItemValueCodec" />, which packs/unpacks a DIFFERENT legacy function -- <c>SetISIUIMValue</c>,
///         <c>function.h:385-436</c>, not the <c>ChangeISValue</c> function this contract actually cites at
///         <c>function.h:345-353</c>; whether the two functions share the same byte layout was NOT confirmed by
///         the C10-remaining-boxes contract). Which of those four bytes (or what combination) a raw 30-60 legacy
///         "value" write actually decodes to is NOT established here -- the same class of octet-mapping gap
///         flagged for the B3-deco IS/IU/IZ/IM tables in a prior workstream. <see cref="ItemValueCodec" /> must
///         NOT be reused for this mapping without a fresh citation confirming <c>ChangeISValue</c> uses the same
///         layout.
///         <see cref="Roll" /> therefore returns the rolled grade as an opaque <see cref="RollResult.EnchantGrade" />
///         integer only -- no attempt is made here to decide which <see cref="Inventory.ItemStack" /> byte field it
///         becomes. Do not invent that mapping when wiring this reward into the placement pipeline; get a fresh
///         legacy-behavior-translator contract (or a direct <c>Server/</c> citation) for it first.
///     </para>
/// </remarks>
public static class CostumeChest76543RewardTable
{
    /// <summary>world.Items id for the Limited Costume Chest itself.</summary>
    public const int BoxItemId = 76543;

    /// <summary>Rental days applied to the granted reward's expiry on every successful open (3-day rental).</summary>
    public const int RentalDays = 3;

    /// <summary>Inclusive lower bound of the 31-outcome enchant-grade roll.</summary>
    public const int EnchantGradeMin = 30;

    /// <summary>Inclusive upper bound of the 31-outcome enchant-grade roll.</summary>
    public const int EnchantGradeMax = 60;

    /// <summary>
    ///     The character's stored previous-tribe code (0=ND, 1=RS, 2=GT -- <c>PlayerRuntimeState.PreviousTribe</c>)
    ///     mapped to its single deterministic reward id. No other previous-tribe value is present -- see
    ///     <see cref="TryResolveRewardId" />.
    /// </summary>
    public static readonly FrozenDictionary<byte, int> RewardIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 76524, [1] = 76525, [2] = 76526 }.ToFrozenDictionary();

    /// <summary>
    ///     Resolves the deterministic (non-random) reward id for <paramref name="previousTribe" />. False for any
    ///     value other than 0/1/2, matching both legacy code paths' eventual rejection of an unrecognized tribe
    ///     (see this type's own remarks for the mechanism divergence between the two paths).
    /// </summary>
    public static bool TryResolveRewardId(byte previousTribe, out int rewardItemId)
    {
        return RewardIdByPreviousTribe.TryGetValue(previousTribe, out rewardItemId);
    }

    /// <summary>The independent 31-outcome uniform enchant-grade roll (30-60 inclusive).</summary>
    public static int RollEnchantGrade(Random random)
    {
        return random.Next(EnchantGradeMin, EnchantGradeMax + 1);
    }

    /// <summary>
    ///     Rolls this box's full outcome for a given opener: the deterministic tribe-mapped reward id plus the
    ///     independently-rolled enchant grade. <see cref="RollResult.Success" /> is false (no draw spent) for an
    ///     unrecognized <paramref name="previousTribe" /> -- see this type's own remarks for why validating the
    ///     tribe before rolling is a deliberate, documented simplification rather than a legacy citation.
    /// </summary>
    public static RollResult Roll(byte previousTribe, Random random)
    {
        if (!TryResolveRewardId(previousTribe, out var rewardItemId))
            return RollResult.Failure;

        return new RollResult(true, rewardItemId, RollEnchantGrade(random));
    }

    /// <summary>Outcome of <see cref="Roll" />. EnchantGrade is only meaningful when Success is true.</summary>
    public readonly record struct RollResult(bool Success, int RewardItemId, int EnchantGrade)
    {
        public static RollResult Failure { get; } = new(false, 0, 0);
    }
}
