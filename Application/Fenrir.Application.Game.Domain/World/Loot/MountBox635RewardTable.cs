using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     The reward-table DATA for item 635 ("15% Mount Box") -- workstream C10-mountbox635, a sibling data file
///     extending <see cref="LootBoxCatalog" /> per that catalog's own "contract-first, never-invent" posture
///     (its own class remarks). The open MECHANISM itself (single-open roll, placement, one-box consumption,
///     double GL_606 audit, zone mirror) is entirely pre-built and untouched by this file:
///     <see cref="Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes.LootBoxOpenResolver" /> and
///     <see cref="Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes.LootBoxUseItemHandler" />, via
///     <see cref="LootBoxRewardResolver.RollUniform" /> -- whose own summary already names item 635's flat 1-in-N
///     roll as its motivating example. This file supplies only the 8 concrete tier-3 mount reward ids the legacy
///     source recovers, plus the <see cref="BoxRewardSpec" /> built from them. See this workstream's wiring
///     manifest for the one-line splice into <see cref="LootBoxCatalog" />'s specs list -- that edit is
///     deliberately NOT made here (concurrent sibling waves touch the same catalog file; the splice is a single
///     serial-integration-pass step).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1939-1941 (author comment: item 635's handler is intentionally
///     unconditional, never <c>#ifdef</c>-gated -- "Do not put this inside #ifdef. This must always be active.")
///     ; :1950-1963 (the live, uniform <c>rand_mir() % 8</c> roll over the 8-entry pool below -- 12.5% each, no
///     skew) ; :2023 (the unconditional early <c>return</c> that makes every later dispatch path, including the
///     bulk-open feature, unreachable for item 635 -- reproduced here by this box id being deliberately absent
///     from <see cref="LootBoxCatalog.BulkOpenWhitelist" />, so a nonzero client-requested open count is silently
///     ignored and exactly one box always opens per request).
///     <para>
///         Reward ids: Server/Header/Protocol/DEFINE.h:156-183 (<c>ANIMAL_NUM_*</c> constants). <c>ANIMAL_NUM_PUMA3</c>
///         (1331, same constant block) is confirmed NOT part of this pool in any of the three legacy
///         implementations the C10-mountbox635 contract read -- deliberately excluded here too.
///     </para>
///     <para>
///         Two structurally dead alternate implementations exist in the same legacy source file (both gated by
///         the never-defined <c>WUSE_ITEM_635</c> macro, confirmed via a tree-wide search for its <c>#define</c>
///         turning up zero matches) and use a different, non-uniform 20-way roll (7 animals at 5% each, the
///         8th/lion at 65% via <c>default</c>) -- Server/ts25zone/S04_MyWork03.cpp:1345-1363 and :6192-6208. That
///         distribution must NOT be carried into this file. See the C10-mountbox635 contract's own Edge cases
///         section for the full citation trail (dead code cited only to positively rule it out).
///     </para>
///     <para>
///         Unrecoverable (contract's own flag, repeated here so it travels with the data): the item's own
///         in-game display name is "15% Mount Box" (source code comment, Server/ts25zone/S04_MyWork03.cpp:884),
///         yet the live roll is a flat uniform 1-in-8 (12.5%) with no 15% figure anywhere in the live handler,
///         and neither dead alternate implementation produces a 15% figure either (5%x7 + 65%x1). No
///         <c>Server/</c> line read for that contract explains the discrepancy -- do not infer a weighted table
///         from the item's own name.
///     </para>
/// </remarks>
public static class MountBox635RewardTable
{
    /// <summary>world.Items id for the "15% Mount Box" itself.</summary>
    public const int BoxItemId = 635;

    // ANIMAL_NUM_* constants, Server/Header/Protocol/DEFINE.h:156-183 -- declaration order matches both the
    // header's own order and the C10-mountbox635 contract's own citation order.
    public const int Tiger3 = 1307;
    public const int Pig3 = 1308;
    public const int Deer3 = 1309;
    public const int Bear3 = 1315;
    public const int Cat3 = 1319;
    public const int Bull3 = 1322;
    public const int Wolf3 = 1325;
    public const int Lion3 = 1328;

    /// <summary>
    ///     The 8-entry uniform pool (Server/ts25zone/S04_MyWork03.cpp:1950-1963): every draw is exactly 1-in-8
    ///     (12.5%), no rarity skew. This is NOT the same pool or distribution as either dead alternate
    ///     implementation (20-way, non-uniform) -- see this type's own remarks.
    /// </summary>
    public static readonly ImmutableArray<int> RewardItemIds =
        [Tiger3, Pig3, Deer3, Bear3, Cat3, Bull3, Wolf3, Lion3];

    /// <summary>
    ///     Builds the <see cref="BoxRewardSpec" /> for <see cref="LootBoxCatalog" /> to register under
    ///     <see cref="BoxItemId" />. No rental/expiry stamp: the contract records none for this box's reward,
    ///     unlike e.g. box 76542's 3-day rental -- <c>rentalDays</c> is left at its default of 0.
    /// </summary>
    public static BoxRewardSpec CreateSpec()
    {
        return BoxRewardSpec.Uniform(BoxItemId, RewardItemIds);
    }
}
