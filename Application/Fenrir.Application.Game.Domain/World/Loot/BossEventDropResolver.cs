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
///     287, 564-568, 1407). When false, the legacy block falls through and the generic tiers still run in addition
///     to whatever this outcome already carries (every other identifier).
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
///         <b>Known gap, intentional:</b> the source behavior contract this class was built from gave exact item
///         catalog ids for only three drops -- identifier 731's item 536, identifier 576's public 20x item 1048
///         (Labyrinth Key), and identifier 746's 30%-chance rare item 93500 -- every other guaranteed/random list
///         below (1404's 9 items, 287's 13-item pool, 576's own 6-item personal list, 756's 3 items, each of
///         564-568's own distinct 6/7-item list, 1407's 8 items, 1408's four pools, and the 746/9001 shared 6-entry
///         pool) was only described by name/shape, never by id, in that contract. Per this repo's "no legacy parity
///         from memory" rule, none of those ids are guessed here -- every such list below is intentionally left
///         empty, with the exact <c>Server/ts25zone/S07_MyGame05.cpp</c> line range still needed cited alongside it,
///         ready for a follow-up <c>legacy-behavior-translator</c> contract to fill in. Every other part of this
///         class (control flow, early-return/fallthrough semantics, CP/War Point/Blood Point amounts, roll
///         thresholds, the public-vs-owned drop distinction, the shared server-process-wide kill tally for
///         identifier 287) is fully modeled from that contract.
///     </para>
///     <para>
///         Identifier 9001 does not correspond to any monster this scheduler's per-region spawn pool can produce
///         today (not present in the seeded <c>world.Monsters</c> catalog as an ordinary spawn) -- it shares
///         identifier 746's ("Virgin Ghost") drop block in the legacy source, so <see cref="Resolve" /> still
///         recognizes it for whenever an event-summon path that can produce it exists.
///     </para>
/// </remarks>
public static class BossEventDropResolver
{
    /// <summary>SANTA_ID (<c>Server/Header/Protocol/DEFINE.h:211</c>) -- a live, unguarded <c>const int</c>, not an undefined macro.</summary>
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

    // ---- Guaranteed/random item lists -- every one of these is an intentionally empty placeholder for a
    // ---- follow-up contract, see this class's own remarks. Do NOT fill these with a guessed id.
    private static readonly IReadOnlyList<DroppedItem> NineItemEventList = []; // :2339-2352
    private static readonly IReadOnlyList<DroppedItem> HolyUnicornPersonalList = []; // :2396-2420 (own 6-item list; the public Labyrinth Key drop IS modeled)
    private static readonly IReadOnlyList<DroppedItem> ThreeItemEventList = []; // :2423-2428
    private static readonly IReadOnlyList<DroppedItem> EliteBossGuaranteedList = []; // :2509-2529
    private static readonly int[] DemonLordItemPool = []; // :2356-2394, 13-item pool
    private static readonly int[] FifteenMinuteBossLowTierPool = []; // :2532-2583, roll < 25, "random animal (tier 10)" helper
    private static readonly int[] FifteenMinuteBossLowMidTierPool = []; // roll [25,100), 2 fixed ids + "random animal (tier 5)" helper
    private static readonly int[] FifteenMinuteBossMidTierPool = []; // roll [100,400), 9-item fixed pool
    private static readonly int[] FifteenMinuteBossHighTierPool = []; // roll >= 400, 3-item fixed pool
    private static readonly int[] SharedRandomPool = []; // :2586-2662, 2 fixed ids + "random elixir" helper + 2 more fixed ids

    private static readonly IReadOnlyList<DroppedItem>[] CustomTimedBossLists =
    [
        [], // 564
        [], // 565
        [], // 566
        [], // 567
        [] // 568
    ];

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
    public static BossDropOutcome Resolve(int monsterId, int demonLordKillTally, Random random,
        WorldDataCache worldData)
    {
        return monsterId switch
        {
            SantaMonsterId => BossDropOutcome.None with { Items = [new DroppedItem(SantaGiftItemId, 1)] },

            NineItemEventBossMonsterId => BossDropOutcome.None with
            {
                Items = NineItemEventList, ContributionPointsGranted = NineItemEventContributionPoints
            },

            DemonLordMonsterId => ResolveDemonLord(demonLordKillTally, random),

            HolyUnicornMonsterId => BossDropOutcome.None with
            {
                Items = HolyUnicornPersonalList,
                PublicItems = [new DroppedItem(LabyrinthKeyItemId, LabyrinthKeyPublicQuantity)]
            },

            ThreeItemEventBossMonsterId => BossDropOutcome.None with { Items = ThreeItemEventList },

            >= CustomTimedBossFirstMonsterId and <= CustomTimedBossLastMonsterId => BossDropOutcome.None with
            {
                Items = CustomTimedBossLists[monsterId - CustomTimedBossFirstMonsterId], SkipGenericTiers = true
            },

            EliteBossMonsterId => BossDropOutcome.None with
            {
                Items = EliteBossGuaranteedList, SkipGenericTiers = true, AnnounceEliteBossDefeat = true
            },

            FifteenMinuteBossMonsterId => ResolveFifteenMinuteBoss(random),

            VirginGhostMonsterId or SharedRandomPoolMonsterId => ResolveSharedRandomPool(monsterId, random, worldData),

            _ => BossDropOutcome.None
        };
    }

    /// <summary>
    ///     Every kill of this identifier exits before the generic tiers regardless of whether an item actually drops
    ///     -- only the 10th (mod) kill (server-process-wide, see <see cref="Resolve" />'s own param remarks) resolves
    ///     one item from the (currently unresolved, see class remarks) 13-item pool.
    /// </summary>
    private static BossDropOutcome ResolveDemonLord(int demonLordKillTally, Random random)
    {
        if (demonLordKillTally <= 0 || demonLordKillTally % DemonLordKillCycle != 0 || DemonLordItemPool.Length == 0)
            return BossDropOutcome.None with { SkipGenericTiers = true };

        var item = DemonLordItemPool[random.Next(DemonLordItemPool.Length)];
        return BossDropOutcome.None with { Items = [new DroppedItem(item, 1)], SkipGenericTiers = true };
    }

    /// <summary>
    ///     No early exit for this identifier either way -- the roll/tier-boundary logic is fully modeled; only the
    ///     four pools' contents (and therefore any actual resolved item) are an open gap, see class remarks.
    /// </summary>
    private static BossDropOutcome ResolveFifteenMinuteBoss(Random random)
    {
        var roll = random.Next(0, FifteenMinuteBossRollCeiling);

        var pool = roll switch
        {
            < FifteenMinuteBossTierBoundaryLow => FifteenMinuteBossLowTierPool,
            < FifteenMinuteBossTierBoundaryMid => FifteenMinuteBossLowMidTierPool,
            < FifteenMinuteBossTierBoundaryHigh => FifteenMinuteBossMidTierPool,
            _ => FifteenMinuteBossHighTierPool
        };

        if (pool.Length == 0)
            return BossDropOutcome.None;

        var resolvedItemId = pool[random.Next(pool.Length)];
        var quantity = resolvedItemId == FifteenMinuteBossDoubleStackItemId ? 2 : 1;
        return BossDropOutcome.None with { Items = [new DroppedItem(resolvedItemId, quantity)] };
    }

    /// <summary>
    ///     Identifiers 746 ("Virgin Ghost") and 9001 share this one block. The routine only ever exits early when
    ///     the shared pool resolves a non-zero item -- since that pool is currently unresolved (class remarks), this
    ///     always takes the documented "resolved item is zero" fallthrough, i.e. <see cref="BossDropOutcome.SkipGenericTiers" />
    ///     stays false and the generic tiers still run, same as legacy would for an actual zero roll.
    /// </summary>
    private static BossDropOutcome ResolveSharedRandomPool(int monsterId, Random random, WorldDataCache worldData)
    {
        var isVirginGhost = monsterId == VirginGhostMonsterId;

        var contributionPoints = isVirginGhost ? 0 : SharedRandomPoolContributionPoints;
        var warPoints = isVirginGhost ? VirginGhostWarPoints : SharedRandomPoolWarPoints;
        var bloodPoints = isVirginGhost ? VirginGhostBloodPoints : SharedRandomPoolBloodPoints;

        List<DroppedItem>? items = null;

        if (SharedRandomPool.Length > 0)
        {
            var resolved = SharedRandomPool[random.Next(SharedRandomPool.Length)];
            if (resolved != 0)
                (items ??= []).Add(new DroppedItem(resolved, 1));
        }

        // The only drop call in this whole tier that validates catalog existence before dropping (Edge cases).
        if (isVirginGhost && worldData.ItemsById.ContainsKey(VirginGhostRareItemId) &&
            LootRandomSource.RandomNumber(random) <= VirginGhostRareItemDropChance)
            (items ??= []).Add(new DroppedItem(VirginGhostRareItemId, 1));

        return new BossDropOutcome(items ?? [], [], contributionPoints, warPoints, bloodPoints, false, false);
    }
}
