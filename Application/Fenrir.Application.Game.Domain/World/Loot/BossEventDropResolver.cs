using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Everything one hardcoded boss/event monster-id match in <see cref="BossEventDropResolver.Resolve" /> produced,
///     to be layered by the caller in front of (never through) <see cref="MonsterDropRoller.Roll" />'s own generic
///     tiers. <see cref="None" /> (every field zeroed/empty, <see cref="SkipGenericTiers" /> false) is
///     what every monster id outside the fixed set <see cref="BossEventDropResolver" /> matches resolves to -- i.e.
///     a complete no-op for the overwhelming majority of kills.
/// </summary>
/// <param name="Items">Guaranteed/rolled items attributed to the killer, spawned exactly like a generic-tier drop.</param>
/// <param name="PublicItems">
///     Ownerless ("public") guaranteed items -- only identifier 576's 20x Labyrinth Key uses this today. Spawned
///     with an empty owner/party name so <see cref="GroundItemEntity.IsClaimableBy" /> rule 3 makes them free-for-all
///     from the moment they land, unlike every other drop in this pipeline.
/// </param>
/// <param name="ContributionPointsGranted">Additional, guaranteed CP grant (identifiers 1404, 9001 only).</param>
/// <param name="WarPointsGranted">Additional, guaranteed War Point grant (identifiers 746, 9001 only).</param>
/// <param name="BloodPointsGranted">Additional, guaranteed DS/Blood Point grant (identifiers 746, 9001 only).</param>
/// <param name="SkipGenericTiers">
///     When true, the matched identifier's legacy block unconditionally <c>return</c>s before ever reaching
///     <c>DROP_MONEY</c> -- <see cref="MonsterDropRoller.Roll" /> must not run at all for this kill (identifiers
///     287, 564-568, 1407, and -- now that its pool is populated and always resolves a positive id -- 746/9001).
///     When false, the legacy block falls through and the generic tiers still run in addition to whatever this
///     outcome already carries (731, 1404, 576, 756, 1408).
/// </param>
/// <param name="AnnounceEliteBossDefeat">Identifier 1407 only -- see <see cref="Zone.AnnounceEliteBossDefeated" />.</param>
public readonly record struct BossDropOutcome(
    IReadOnlyList<DroppedItem> Items,
    IReadOnlyList<DroppedItem> PublicItems,
    int ContributionPointsGranted,
    int WarPointsGranted,
    int BloodPointsGranted,
    bool SkipGenericTiers,
    bool AnnounceEliteBossDefeat)
{
    public static readonly BossDropOutcome None = new([], [], 0, 0, 0, false, false);
}

/// <summary>
///     Ports the ~8 hardcoded, unconditionally-compiled boss/event drop blocks <see cref="MonsterDropRoller" />'s own
///     (now-corrected, see its class remarks) doc comment used to mischaracterize as dead code --
///     <c>Server/ts25zone/S07_MyGame05.cpp:2333-2662</c>, the section immediately preceding <c>DROP_MONEY</c>. Only
///     <c>mIndex==1400||1406</c> ("BOSS POPUP", <c>:2256-2331</c>) is genuinely commented out in that range; every
///     identifier <see cref="Resolve" /> matches below is a live, unguarded <c>if</c> that runs on every
///     <c>ReleaseEU33</c> build.
/// </summary>
/// <remarks>
///     <para>
///         This class owns the control flow -- early-return/fallthrough semantics, CP/War Point/Blood Point
///         amounts, roll thresholds, the public-vs-owned drop distinction, the shared server-process-wide kill
///         tally for identifier 287. The item-id DATA each block drops lives in <see cref="BossDropCatalog" />
///         (guaranteed lists and index-picked pools) and <see cref="BossDropHelperResolver" /> (the per-kill
///         random animal/elixir pickers); every id in both is grounded in the C4 boss-drop behavior contract's
///         own drop blocks (<c>Server/ts25zone/S07_MyGame05.cpp:2333-2662</c>, with the animal/elixir tables at
///         <c>S07_MyGame03.cpp:7308-7359</c>), cited per-list at each member there.
///     </para>
///     <para>
///         Identifier 9001 does not correspond to any monster this scheduler's per-region spawn pool can produce
///         today (not present in the seeded <c>world.Monsters</c> catalog as an ordinary spawn) -- it shares
///         identifier 746's ("Virgin Ghost") drop block in the legacy source, so <see cref="Resolve" /> still
///         recognizes it for whenever an event-summon path that can produce it exists.
///     </para>
///     <para>
///         <b>Not modeled (pre-existing, out of C4's data-fill scope):</b> the legacy loot-tribe filter the
///         746/9001 shared-pool drop and the 746 rare bonus pass (both equal to the killer's tribe) -- Fenrir's
///         <see cref="DroppedItem" /> carries no tribe filter and every drop here is attributed to the killer
///         through the same owner-name path as any other drop; see the contract's own "emitter/argument
///         semantics not fully re-read" edge case.
///     </para>
/// </remarks>
public static class BossEventDropResolver
{
    /// <summary>
    ///     SANTA_ID (<c>Server/Header/Protocol/DEFINE.h:211</c>) -- a live, unguarded <c>const int</c>, not an undefined
    ///     macro.
    /// </summary>
    public const int SantaMonsterId = 731;

    private const int SantaGiftItemId = 536;

    public const int NineItemEventBossMonsterId = 1404;
    private const int NineItemEventContributionPoints = 50;

    /// <summary>"Demon Lord" -- see <see cref="Resolve" />'s own remarks for the shared kill-tally contract.</summary>
    public const int DemonLordMonsterId = 287;

    private const int DemonLordKillCycle = 10;

    public const int HolyUnicornMonsterId = 576;
    private const int LabyrinthKeyItemId = 1048;
    private const int LabyrinthKeyPublicQuantity = 20;

    public const int ThreeItemEventBossMonsterId = 756;

    /// <summary>
    ///     "Custom timed bosses" ("1 HOUR BOSS" through "24 HOURS BOSS" in-source) -- the same range
    ///     <c>MonsterSpawnScheduler.IsPersistedBossMonster</c> (private) already recognizes for persisted
    ///     respawn-timer tracking.
    /// </summary>
    public const int CustomTimedBossFirstMonsterId = 564;

    public const int CustomTimedBossLastMonsterId = 568;

    public const int EliteBossMonsterId = 1407;

    public const int FifteenMinuteBossMonsterId = 1408;

    private const int FifteenMinuteBossRollCeiling = 1000; // roll is 0..999 inclusive
    private const int FifteenMinuteBossTierBoundaryLow = 25;
    private const int FifteenMinuteBossTierBoundaryMid = 100;
    private const int FifteenMinuteBossTierBoundaryHigh = 400;

    /// <summary>The one item id given verbatim in the source contract for identifier 1408: doubles its own drop quantity.</summary>
    private const int FifteenMinuteBossDoubleStackItemId = 1449;

    public const int VirginGhostMonsterId = 746;
    public const int SharedRandomPoolMonsterId = 9001;

    private const int VirginGhostRareItemId = 93500;

    /// <summary>
    ///     30% of <see cref="LootRandomSource.RandomNumber" />'s [1, 1_000_000] range -- the exact RNG call this
    ///     percentage compares against was not itself re-opened in the source contract (only "a separate 30%-chance
    ///     ... roll" was given), so this reuses the same <c>MyUtil::RandomNumber()</c>-shaped check every other roll
    ///     in this immediate source range already uses rather than a fresh <c>rand() % 100</c>-style guess --
    ///     flagged for re-verification, not independently confirmed against the literal line.
    /// </summary>
    private const int VirginGhostRareItemDropChance = 300_000;

    private const int VirginGhostWarPoints = 2;
    private const int VirginGhostBloodPoints = 2;
    private const int SharedRandomPoolContributionPoints = 5;
    private const int SharedRandomPoolWarPoints = 3;
    private const int SharedRandomPoolBloodPoints = 6;

    /// <summary>
    ///     Resolves the boss/event tier for one kill. Runs unconditionally for the fixed fifteen (well, ten)
    ///     identifiers below -- no cooldown, zone, or inventory-space gate exists in the cited source range beyond
    ///     what the caller already checked before ever reaching this tier (a resolvable killer, see
    ///     <c>Server/ts25zone/S07_MyGame05.cpp:2021-2027</c>).
    /// </summary>
    /// <param name="demonLordKillTally">
    ///     Only meaningful for <see cref="DemonLordMonsterId" />: the caller's own already-incremented,
    ///     process-wide-shared tally for this exact kill (legacy's function-local static counter, shared across
    ///     every zone/instance of this monster and every killer server-process-wide -- see
    ///     <see cref="MonsterSpawnScheduler" />'s own field for where that shared state actually lives).
    ///     Ignored for every other identifier.
    /// </param>
    /// <param name="catalog">The item-id lists each block drops -- <see cref="BossDropCatalog.Default" /> in production.</param>
    public static BossDropOutcome Resolve(int monsterId, int demonLordKillTally, Random random,
        WorldDataCache worldData, BossDropCatalog catalog)
    {
        return monsterId switch
        {
            SantaMonsterId => BossDropOutcome.None with { Items = [new DroppedItem(SantaGiftItemId, 1)] },

            NineItemEventBossMonsterId => BossDropOutcome.None with
            {
                Items = catalog.NineItemEventList, ContributionPointsGranted = NineItemEventContributionPoints
            },

            DemonLordMonsterId => ResolveDemonLord(demonLordKillTally, random, catalog),

            HolyUnicornMonsterId => BossDropOutcome.None with
            {
                Items = catalog.HolyUnicornPersonalList,
                PublicItems = [new DroppedItem(LabyrinthKeyItemId, LabyrinthKeyPublicQuantity)]
            },

            ThreeItemEventBossMonsterId => BossDropOutcome.None with { Items = catalog.ThreeItemEventList },

            >= CustomTimedBossFirstMonsterId and <= CustomTimedBossLastMonsterId =>
                ResolveCustomTimedBoss(monsterId, catalog),

            EliteBossMonsterId => BossDropOutcome.None with
            {
                Items = catalog.EliteBossGuaranteedList, SkipGenericTiers = true, AnnounceEliteBossDefeat = true
            },

            FifteenMinuteBossMonsterId => ResolveFifteenMinuteBoss(random, catalog),

            VirginGhostMonsterId or SharedRandomPoolMonsterId =>
                ResolveSharedRandomPool(monsterId, random, worldData, catalog),

            _ => BossDropOutcome.None
        };
    }

    /// <summary>
    ///     Identifiers 564-568 (:2431-2506): each drops its own distinct guaranteed list in order, then always
    ///     aborts before the generic tiers. The <c>TryGetValue</c> miss branch is defensive only -- every id in
    ///     the range has a seeded list.
    /// </summary>
    private static BossDropOutcome ResolveCustomTimedBoss(int monsterId, BossDropCatalog catalog)
    {
        return catalog.CustomTimedBossLists.TryGetValue(monsterId, out var list)
            ? BossDropOutcome.None with { Items = list, SkipGenericTiers = true }
            : BossDropOutcome.None with { SkipGenericTiers = true };
    }

    /// <summary>
    ///     Every kill of this identifier exits before the generic tiers regardless of whether an item actually drops
    ///     -- only the 10th (mod) kill (server-process-wide, see <see cref="Resolve" />'s own param remarks) resolves
    ///     one item uniformly from the thirteen-entry pool.
    /// </summary>
    private static BossDropOutcome ResolveDemonLord(int demonLordKillTally, Random random, BossDropCatalog catalog)
    {
        var pool = catalog.DemonLordItemPool;
        if (demonLordKillTally <= 0 || demonLordKillTally % DemonLordKillCycle != 0 || pool.Length == 0)
            return BossDropOutcome.None with { SkipGenericTiers = true };

        var item = pool[random.Next(pool.Length)];
        return BossDropOutcome.None with { Items = [new DroppedItem(item, 1)], SkipGenericTiers = true };
    }

    /// <summary>
    ///     No early exit for this identifier either way (1408 falls through to the generic table in addition to
    ///     its own drop): the standard-generator weighted tier roll picks a pool, then one item is picked uniformly
    ///     from it. The two lowest tiers' pools are built per kill from a helper-drawn animal
    ///     (<see cref="BossDropHelperResolver" />); the two upper tiers are wholly fixed (<see cref="BossDropCatalog" />).
    /// </summary>
    private static BossDropOutcome ResolveFifteenMinuteBoss(Random random, BossDropCatalog catalog)
    {
        var roll = random.Next(0, FifteenMinuteBossRollCeiling);

        int resolvedItemId;
        if (roll < FifteenMinuteBossTierBoundaryLow)
        {
            // Low tier (2.5%): a single random tier-2 animal.
            Span<int> pool = [BossDropHelperResolver.ResolveRandomTier2Animal(random)];
            resolvedItemId = pool[random.Next(pool.Length)];
        }
        else if (roll < FifteenMinuteBossTierBoundaryMid)
        {
            // Low-mid tier (7.5%): two fixed ids plus a random tier-1 animal, in that order.
            var fixedIds = catalog.FifteenMinuteBossLowMidFixedIds;
            Span<int> pool = [fixedIds[0], fixedIds[1], BossDropHelperResolver.ResolveRandomTier1Animal(random)];
            resolvedItemId = pool[random.Next(pool.Length)];
        }
        else
        {
            var pool = roll < FifteenMinuteBossTierBoundaryHigh
                ? catalog.FifteenMinuteBossMidTierPool
                : catalog.FifteenMinuteBossHighTierPool;
            resolvedItemId = pool[random.Next(pool.Length)];
        }

        if (resolvedItemId == 0)
            return BossDropOutcome.None;

        var quantity = resolvedItemId == FifteenMinuteBossDoubleStackItemId ? 2 : 1;
        return BossDropOutcome.None with { Items = [new DroppedItem(resolvedItemId, quantity)] };
    }

    /// <summary>
    ///     Identifiers 746 ("Virgin Ghost") and 9001 share this one block. Draw order matches the contract's own
    ///     block order: build the shared six-entry pool (its fourth slot a per-kill random elixir), then -- 746
    ///     only -- the 30%-chance rare bonus, then the uniform shared-pool pick. Because every pool entry is a
    ///     positive id the pick always resolves non-zero, so this identifier's legacy "abort before the generic
    ///     table" fires on every kill (<see cref="BossDropOutcome.SkipGenericTiers" /> = true) -- unlike the empty-
    ///     pool placeholder this replaced, which always fell through.
    /// </summary>
    private static BossDropOutcome ResolveSharedRandomPool(int monsterId, Random random, WorldDataCache worldData,
        BossDropCatalog catalog)
    {
        var isVirginGhost = monsterId == VirginGhostMonsterId;

        var contributionPoints = isVirginGhost ? 0 : SharedRandomPoolContributionPoints;
        var warPoints = isVirginGhost ? VirginGhostWarPoints : SharedRandomPoolWarPoints;
        var bloodPoints = isVirginGhost ? VirginGhostBloodPoints : SharedRandomPoolBloodPoints;

        // Six-entry pool with the per-kill random elixir spliced into its documented slot (index 3): the three
        // fixed ids before it, the elixir, then the two fixed ids after it. Stack-allocated -- no per-kill heap.
        var fixedIds = catalog.SharedRandomPoolFixedIds;
        Span<int> pool =
        [
            fixedIds[0], fixedIds[1], fixedIds[2],
            BossDropHelperResolver.ResolveRandomElixir(random),
            fixedIds[3], fixedIds[4]
        ];

        List<DroppedItem>? items = null;

        // 746 only: the 30%-chance rare bonus, guarded by the one catalog-existence check in this whole tier (Edge
        // cases). Rolled BEFORE the shared-pool pick, matching the contract's block order.
        if (isVirginGhost && worldData.ItemsById.ContainsKey(VirginGhostRareItemId) &&
            LootRandomSource.RandomNumber(random) <= VirginGhostRareItemDropChance)
            (items ??= []).Add(new DroppedItem(VirginGhostRareItemId, 1));

        // Both: pick one item uniformly from the shared pool and drop it, then abort. resolved is always non-zero
        // (every pool entry is positive), so the abort effectively always fires -- see this method's remarks.
        var resolved = pool[random.Next(pool.Length)];
        var abortGenericTiers = resolved != 0;
        if (abortGenericTiers)
            (items ??= []).Add(new DroppedItem(resolved, 1));

        return new BossDropOutcome(items ?? [], [], contributionPoints, warPoints, bloodPoints, abortGenericTiers,
            false);
    }
}
