using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     The item-id data behind every fixed <see cref="BossEventDropResolver" /> block, materialized once
///     (process-wide, immutable) and read -- never mutated -- on the tick-thread loot path. Splits the DATA out
///     of <see cref="BossEventDropResolver" />'s already-verified control flow: the resolver owns the
///     thresholds/CP-WP-BP amounts/early-return-vs-fallthrough semantics and calls into these lists; this class
///     owns only which item ids each list holds.
/// </summary>
/// <remarks>
///     Every id below is grounded in the C4 boss-drop behavior contract's own drop blocks
///     (<c>Server/ts25zone/S07_MyGame05.cpp:2333-2662</c>), cited per-list at each member. This is a Domain-owned
///     static asset, not a SQL-seeded table: the legacy source hardcodes these blocks in C++, so they live in
///     memory here the same way (built once, no DB round trip, no admin edit surface), matching how
///     <see cref="Quests.QuestCatalog" /> is a boot-time-built read-only index rather than a per-request query.
///     <para>
///         <b>Not held here (resolved per kill instead):</b> the "random animal (tier 1/2)" entries in identifier
///         1408's low/low-mid tiers and the "random elixir" slot in the 746/9001 shared pool -- those are a fresh
///         uniform draw on every kill, so they are a <see cref="BossDropHelperResolver" /> invocation, not a
///         seeded list entry. The truly-fixed ids that surround those dynamic slots DO live here (see
///         <see cref="FifteenMinuteBossLowMidFixedIds" /> and <see cref="SharedRandomPoolFixedIds" />).
///     </para>
///     <para>
///         Guaranteed drop lists are typed <see cref="IReadOnlyList{T}" /> (backed by an
///         <see cref="ImmutableArray{T}" /> boxed exactly once at build) so
///         <see cref="BossEventDropResolver.Resolve" /> can hand one straight to
///         <see cref="BossDropOutcome.Items" /> as a plain reference copy -- zero per-kill allocation. Pools
///         picked from by index stay <see cref="ImmutableArray{T}" /> of <see cref="int" />, indexed directly with
///         no boxing.
///     </para>
/// </remarks>
public sealed class BossDropCatalog
{
    /// <summary>The elixir slot's index inside the 6-entry 746/9001 shared pool (see <see cref="SharedRandomPoolFixedIds" />).</summary>
    public const int SharedRandomPoolElixirSlotIndex = 3;

    private BossDropCatalog()
    {
        // Identifier 1404 (:2339-2352): nine guaranteed drops in order, item 724 at quantity two.
        NineItemEventList = Guaranteed(
        [
            (1103, 1), (601, 1), (602, 1), (2249, 1), (1073, 1), (724, 2), (1437, 1), (1422, 1), (1448, 1)
        ]);

        // Identifier 576 (:2396-2420): six guaranteed private drops in order, item 724 at quantity two. (The
        // public 20x Labyrinth Key 1048 is modeled separately in BossEventDropResolver, not here.)
        HolyUnicornPersonalList = Guaranteed(
        [
            (1073, 1), (698, 1), (1448, 1), (2003, 1), (724, 2), (8112, 1)
        ]);

        // Identifier 756 (:2423-2428): three guaranteed drops in order.
        ThreeItemEventList = Guaranteed([(1073, 1), (1447, 1), (723, 1)]);

        // Identifier 1407 (:2509-2529): eight guaranteed drops in order, item 724 at quantity two.
        EliteBossGuaranteedList = Guaranteed(
        [
            (1073, 1), (698, 1), (1448, 1), (724, 2), (2003, 1), (8112, 1), (1437, 1), (1422, 1)
        ]);

        // Identifiers 564-568 (:2431-2506): one distinct guaranteed list per id, dropped in order.
        CustomTimedBossLists = new Dictionary<int, IReadOnlyList<DroppedItem>>
        {
            [564] = Guaranteed([(8109, 1), (1166, 1), (1243, 1), (696, 1), (8102, 1), (1237, 1)]),
            [565] = Guaranteed([(1126, 1), (2001, 1), (696, 1), (92286, 1), (2477, 1), (1103, 1)]),
            [566] = Guaranteed([(8113, 1), (2001, 1), (1103, 1), (1072, 1), (1178, 1), (1179, 1)]),
            [567] = Guaranteed([(8113, 1), (2002, 1), (1103, 1), (1072, 1), (92286, 1), (92287, 1)]),
            [568] = Guaranteed([(8112, 1), (1434, 1), (8113, 1), (2002, 1), (1103, 1), (1073, 1), (828, 1)])
        }.ToFrozenDictionary();

        // Identifier 287 (:2356-2394): the thirteen-entry pool one item is uniformly picked from every tenth kill.
        DemonLordItemPool = [8109, 1492, 8102, 1045, 1019, 1020, 1021, 1022, 1023, 1017, 1018, 1092, 1093];

        // Identifier 1408 (:2532-2583) fixed tiers. Low (roll < 25) and low-mid (roll [25,100)) are built per kill
        // from a helper-drawn animal, so only their fixed surrounding ids sit here; mid/high are wholly fixed.
        FifteenMinuteBossLowMidFixedIds = [1178, 92286]; // low-mid prefix; a random tier-1 animal is appended per kill
        FifteenMinuteBossMidTierPool = [2249, 602, 1449, 724, 8102, 1023, 1022, 1422, 1072];
        FifteenMinuteBossHighTierPool = [695, 696, 698];

        // Identifiers 746/9001 (:2586-2662): the truly-fixed members of the 6-entry shared pool, in pool order but
        // WITHOUT the random-elixir slot that sits at index SharedRandomPoolElixirSlotIndex between the third and
        // fourth fixed id -- BossEventDropResolver splices the per-kill elixir draw in at that position.
        SharedRandomPoolFixedIds = [1023, 1022, 8102, 695, 696];
    }

    /// <summary>The one process-wide instance, shared by every zone via <see cref="Monsters.MonsterSpawnScheduler" />.</summary>
    public static BossDropCatalog Default { get; } = new();

    /// <summary>Identifier 1404's nine-item guaranteed list (attributed to the killer).</summary>
    public IReadOnlyList<DroppedItem> NineItemEventList { get; }

    /// <summary>Identifier 576's six-item personal guaranteed list (the public Labyrinth Key drop is modeled elsewhere).</summary>
    public IReadOnlyList<DroppedItem> HolyUnicornPersonalList { get; }

    /// <summary>Identifier 756's three-item guaranteed list.</summary>
    public IReadOnlyList<DroppedItem> ThreeItemEventList { get; }

    /// <summary>Identifier 1407's eight-item guaranteed list.</summary>
    public IReadOnlyList<DroppedItem> EliteBossGuaranteedList { get; }

    /// <summary>Guaranteed lists for the timed bosses 564-568, keyed by monster id.</summary>
    public FrozenDictionary<int, IReadOnlyList<DroppedItem>> CustomTimedBossLists { get; }

    /// <summary>Identifier 287's thirteen-entry pool (one item uniformly picked on a drop kill).</summary>
    public ImmutableArray<int> DemonLordItemPool { get; }

    /// <summary>Identifier 1408's low-mid tier fixed prefix (a random tier-1 animal is appended per kill).</summary>
    public ImmutableArray<int> FifteenMinuteBossLowMidFixedIds { get; }

    /// <summary>Identifier 1408's mid tier (roll [100,400)) nine-item pool (one uniformly picked).</summary>
    public ImmutableArray<int> FifteenMinuteBossMidTierPool { get; }

    /// <summary>Identifier 1408's high tier (roll >= 400) three-item pool (one uniformly picked).</summary>
    public ImmutableArray<int> FifteenMinuteBossHighTierPool { get; }

    /// <summary>
    ///     Identifiers 746/9001's five fixed shared-pool ids, in pool order minus the random-elixir slot -- the
    ///     first three (indices 0-2) precede the elixir slot, the last two (indices 3-4) follow it. See
    ///     <see cref="SharedRandomPoolElixirSlotIndex" />.
    /// </summary>
    public ImmutableArray<int> SharedRandomPoolFixedIds { get; }

    private static IReadOnlyList<DroppedItem> Guaranteed(ReadOnlySpan<(int ItemId, int Quantity)> entries)
    {
        var builder = ImmutableArray.CreateBuilder<DroppedItem>(entries.Length);
        foreach (var (itemId, quantity) in entries)
            builder.Add(new DroppedItem(itemId, quantity));

        // Boxed to IReadOnlyList exactly once here, so per-kill assignment to BossDropOutcome.Items is a plain
        // reference copy (no per-kill allocation) -- see this class's own remarks.
        return builder.MoveToImmutable();
    }
}
