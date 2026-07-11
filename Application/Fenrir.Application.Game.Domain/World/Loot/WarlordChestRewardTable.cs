using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Reward-table DATA and roll primitive for items 1378 (Sky/Heaven Warlord Chest, elite tier) and 1379
///     (Earth Warlord Chest, rare tier) -- workstream C10-remaining-boxes, mirroring the legacy shared helper
///     <c>ReturnWarlord</c>. Like <see cref="CostumeChest76543RewardTable" />, these do NOT fit any existing
///     <see cref="BoxRewardKind" />: the reward pool is keyed by the opening character's own stored previous-
///     tribe code, which <see cref="BoxRewardSpec.RollRewardId(Random)" /> has no way to receive, and both items
///     additionally require a level/rebirth-tier ("G12") precondition the shared open mechanism does not check
///     for any other box. This file therefore supplies pure DATA plus a self-contained, fully-tested roll
///     primitive (<see cref="TryRollReward" />) rather than a <see cref="BoxRewardSpec" />; wiring it into the
///     shared open mechanism requires extending that mechanism's shape first -- see this workstream's wiring
///     manifest for the precise required diff, deliberately NOT made here (this file only adds new types, per the
///     "new files only" rule for this pass).
/// </summary>
/// <remarks>
///     Réf. C++ (C10-remaining-boxes contract) : Server/ts25zone/S04_MyWork03.cpp:5539-5614 (the shared 1378/1379
///     case block: on-hand-quantity gate, G12 level gate, tier-selection switch, <c>ReturnWarlord</c> call,
///     result-validity gate, grant call) -- the ONLY reachable code path for either id, since neither appears in
///     <see cref="LootBoxCatalog.BulkOpenWhitelist" /> (Server/ts25zone/S04_MyWork03.cpp:1291-1316, confirmed
///     absent). Pool data: Server/ts25zone/S07_MyGame03.cpp:5243-5423 (<c>ReturnWarlord</c>), rare tier (1379)
///     at :5262-5304, elite tier (1378) at :5306-5407 including the eight dead/commented-out ids per tribe at
///     :5325-5332, :5353-5360, :5381-5388 (never compiled into any build -- NOT reproduced here). Tribe/tier
///     validity guards: :5248-5258 (non-positive sentinel for a tribe outside 0-2 or a tier outside 1/2). G12
///     gate: Server/Header/Protocol/DEFINE.h:605 (<c>MAX_LIMIT_HIGH_LEVEL_NUM</c> = 12), checked against the
///     character's <c>PlayerRuntimeState.Level2</c> ("aLevel2") field before any reward is rolled
///     (Server/ts25zone/S04_MyWork03.cpp:5552-5556).
///     <para>
///         Grant shape per the contract's own Side effects section: quantity 0, "value" 0, no expiry (all 87xxx
///         ids fall outside the recognized rentable-id range, Server/Header/function.h:2216-2264, so these
///         rewards are always permanent) -- no <see cref="BoxRewardSpec.RentalDays" />-equivalent applies here at
///         all, unlike 76543.
///     </para>
///     <para>
///         <b>Deliberately excluded, confirmed dead (do not reproduce):</b> 8 of the 22 authored 1378 reward ids
///         per tribe exist only as plain commented-out source lines, never compiled into any build configuration.
///         Only the 14 LIVE ids per tribe are modeled in <see cref="ElitePoolsByPreviousTribe" /> below. A
///         related flag variable adjacent to <c>ReturnWarlord</c> in source (<c>mGoodWarlord</c>) has no
///         read/write site anywhere in the codebase and is not a gating condition for this table; a similarly
///         named but unrelated, genuinely live variable (<c>mGoodWarlord2</c>) belongs to a separate item-upgrade
///         draw system entirely out of scope here.
///     </para>
/// </remarks>
public static class WarlordChestRewardTable
{
    /// <summary>world.Items id for the Sky/Heaven Warlord Chest (elite tier).</summary>
    public const int SkyChestBoxItemId = 1378;

    /// <summary>world.Items id for the Earth Warlord Chest (rare tier).</summary>
    public const int EarthChestBoxItemId = 1379;

    /// <summary>
    ///     <c>MAX_LIMIT_HIGH_LEVEL_NUM</c> (Server/Header/Protocol/DEFINE.h:605) -- the minimum
    ///     <c>PlayerRuntimeState.Level2</c> ("aLevel2"/"G12") value required before either chest may roll a
    ///     reward at all. Checked independently of, and before, the tribe/pool lookup below.
    /// </summary>
    public const short MinimumLevel2 = 12;

    /// <summary>
    ///     1379 (Earth Chest, rare tier) pool: 9 ids per previous-tribe code (0=ND, 1=RS, 2=GT), each equally
    ///     likely (Server/ts25zone/S07_MyGame03.cpp:5262-5304).
    /// </summary>
    public static readonly FrozenDictionary<byte, ImmutableArray<int>> RarePoolsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] = [87013, 87014, 87015, 87016, 87017, 87018, 87019, 87020, 87008],
            [1] = [87034, 87035, 87036, 87037, 87038, 87039, 87040, 87041, 87029],
            [2] = [87055, 87056, 87057, 87058, 87059, 87060, 87061, 87062, 87050]
        }.ToFrozenDictionary();

    /// <summary>
    ///     1378 (Sky/Heaven Chest, elite tier) pool: the 14 LIVE ids per previous-tribe code out of 22 authored
    ///     (8 dead/commented-out per tribe, excluded -- see this type's own remarks), each equally likely with no
    ///     weighting between the two contiguous live sub-ranges within a tribe (Server/ts25zone/S07_MyGame03.cpp:5306-5407).
    /// </summary>
    public static readonly FrozenDictionary<byte, ImmutableArray<int>> ElitePoolsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] =
            [
                87071, 87072, 87073, 87074, 87075, 87076,
                87077, 87078, 87079, 87080, 87081, 87082, 87083, 87084
            ],
            [1] =
            [
                87093, 87094, 87095, 87096, 87097, 87098,
                87099, 87100, 87101, 87102, 87103, 87104, 87105, 87106
            ],
            [2] =
            [
                87115, 87116, 87117, 87118, 87119, 87120,
                87121, 87122, 87123, 87124, 87125, 87126, 87127, 87128
            ]
        }.ToFrozenDictionary();

    /// <summary>
    ///     The G12 level/rebirth-tier gate shared by both chests -- must be checked (and must fail closed) before
    ///     any pool lookup or roll is attempted, per the contract's own Preconditions/Edge cases sections.
    /// </summary>
    public static bool MeetsLevelGate(short level2)
    {
        return level2 >= MinimumLevel2;
    }

    /// <summary>
    ///     Rolls one reward id for <paramref name="boxItemId" /> (must be <see cref="SkyChestBoxItemId" /> or
    ///     <see cref="EarthChestBoxItemId" />) and <paramref name="previousTribe" /> (must be 0, 1, or 2). False
    ///     for any other box id, any other tribe code, or an empty pool -- mirroring <c>ReturnWarlord</c>'s own
    ///     non-positive sentinel for an invalid tribe/tier argument. Does NOT check the G12 level gate itself --
    ///     callers must call <see cref="MeetsLevelGate" /> first, since that gate is evaluated before any pool
    ///     lookup in legacy and is conceptually independent of which pool would have been drawn.
    /// </summary>
    public static bool TryRollReward(int boxItemId, byte previousTribe, Random random, out int rewardItemId)
    {
        var pools = boxItemId switch
        {
            EarthChestBoxItemId => RarePoolsByPreviousTribe,
            SkyChestBoxItemId => ElitePoolsByPreviousTribe,
            _ => null
        };

        if (pools is null || !pools.TryGetValue(previousTribe, out var pool) || pool.IsDefaultOrEmpty)
        {
            rewardItemId = 0;
            return false;
        }

        rewardItemId = LootBoxRewardResolver.RollUniform(random, pool);
        return true;
    }
}
