using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     The Lucky Ticket family's recovered draw data (workstream C9-tickets-tower follow-up): per-ticket
///     thresholds, the roll/level tier cascade (including the deployment-stage-gated top tier), the
///     evolution-tier-aware item-level window, the fixed family serial, and the end-to-end draw retry.
/// </summary>
public class LuckyTicketRewardResolverTests
{
    // ---- TryGetThresholds ----------------------------------------------------------------------------

    [Theory]
    [InlineData(1035, 1, 300)]
    [InlineData(1036, 2, 400)]
    [InlineData(1037, 3, 500)]
    public void TryGetThresholds_KnownTicket_ReturnsItsOwnPair(int ticketItemId, int expectedFirst,
        int expectedSecond)
    {
        Assert.True(LuckyTicketRewardResolver.TryGetThresholds(ticketItemId, out var first, out var second));
        Assert.Equal(expectedFirst, first);
        Assert.Equal(expectedSecond, second);
    }

    [Fact]
    public void TryGetThresholds_UnknownTicket_ReturnsFalse()
    {
        Assert.False(LuckyTicketRewardResolver.TryGetThresholds(17124, out _, out _));
    }

    // ---- ResolveFamilySerial ---------------------------------------------------------------------------

    [Theory]
    [InlineData(1035, 100000001)]
    [InlineData(1036, 100000002)]
    [InlineData(1037, 100000003)]
    public void ResolveFamilySerial_MatchesTheFixedPerTicketConstant(int ticketItemId, int expectedSerial)
    {
        Assert.Equal(expectedSerial, LuckyTicketRewardResolver.ResolveFamilySerial(ticketItemId));
    }

    // ---- ResolveItemLevelWindow (GetLLevel/GetHLevel) -------------------------------------------------

    [Fact]
    public void ResolveItemLevelWindow_BelowFirstEvolutionTier_IsPlusMinus5AroundLevel()
    {
        var (low, high) = LuckyTicketRewardResolver.ResolveItemLevelWindow(50, 0);
        Assert.Equal(45, low);
        Assert.Equal(55, high);
    }

    [Fact]
    public void ResolveItemLevelWindow_BelowFirstEvolutionTier_LowClampsToFloorOf1()
    {
        var (low, high) = LuckyTicketRewardResolver.ResolveItemLevelWindow(3, 0);
        Assert.Equal(1, low);
        Assert.Equal(8, high);
    }

    [Fact]
    public void ResolveItemLevelWindow_BelowFirstEvolutionTier_HighClampsToMaxItemLevel145()
    {
        var (low, high) = LuckyTicketRewardResolver.ResolveItemLevelWindow(142, 0);
        Assert.Equal(137, low);
        Assert.Equal(145, high);
    }

    [Fact]
    public void ResolveItemLevelWindow_EvolutionTierReached_CollapsesToTheSingleFixedSum()
    {
        var (low, high) = LuckyTicketRewardResolver.ResolveItemLevelWindow(145, 1);
        Assert.Equal(146, low);
        Assert.Equal(146, high);
    }

    // ---- ResolveTier (roll/level cascade) --------------------------------------------------------------

    [Theory]
    // Bracket 1: roll < firstThreshold.
    [InlineData(0, 4, false, LuckyTicketRewardResolver.Common)]
    [InlineData(0, 5, false, LuckyTicketRewardResolver.Unique)]
    [InlineData(0, 44, false, LuckyTicketRewardResolver.Unique)]
    [InlineData(0, 45, false, LuckyTicketRewardResolver.Rare)]
    [InlineData(0, 99, false, LuckyTicketRewardResolver.Rare)]
    [InlineData(0, 100, false, LuckyTicketRewardResolver.Rare)] // elite disabled -> falls back to Rare
    [InlineData(0, 100, true, LuckyTicketRewardResolver.Elite)] // elite enabled -> promotes to Elite
    // Bracket 2: firstThreshold <= roll < secondThreshold -- never reaches Elite, regardless of level/flag.
    [InlineData(150, 4, true, LuckyTicketRewardResolver.Common)]
    [InlineData(150, 5, true, LuckyTicketRewardResolver.Unique)]
    [InlineData(150, 44, true, LuckyTicketRewardResolver.Unique)]
    [InlineData(150, 45, true, LuckyTicketRewardResolver.Rare)]
    [InlineData(150, 200, true, LuckyTicketRewardResolver.Rare)]
    // Bracket 3: secondThreshold <= roll < 9000.
    [InlineData(8000, 4, true, LuckyTicketRewardResolver.Common)]
    [InlineData(8000, 5, true, LuckyTicketRewardResolver.Unique)]
    [InlineData(8000, 200, true, LuckyTicketRewardResolver.Unique)]
    // Bracket 4: roll >= 9000 -- always the lowest tier, regardless of level or the elite flag.
    [InlineData(9000, 200, true, LuckyTicketRewardResolver.Common)]
    [InlineData(9999, 200, true, LuckyTicketRewardResolver.Common)]
    public void ResolveTier_MatchesTheLegacyRollLevelCascade(int roll, int level1, bool eliteTierEnabled,
        int expectedTier)
    {
        // Uses ticket 1037's own thresholds (3/500) -- the widest bracket 1 -- so the InlineData rolls above
        // line up cleanly against all four brackets without per-row threshold bookkeeping.
        Assert.Equal(expectedTier, LuckyTicketRewardResolver.ResolveTier(roll, level1, eliteTierEnabled, 3, 500));
    }

    [Fact]
    public void ShippedProductionEliteTierEnabled_IsFalse_MatchingServerInfoIni()
    {
        Assert.False(LuckyTicketRewardResolver.ShippedProductionEliteTierEnabled);
    }

    // ---- TryDraw (end-to-end, delegating to GeneralItemDropResolver) ----------------------------------

    private static ItemRowDto EligibleItem(int itemId, int level, byte type, byte sort)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Level = (short)level,
            Type = type,
            Sort = sort,
            CheckMonsterDrop = 2,
            CheckAvatarTrade = 2,
            CheckSetItem = 1,
            EquipInfo1 = 1
        };
    }

    [Fact]
    public void TryDraw_UnknownTicketId_ReturnsFalseWithoutTouchingTheCatalog()
    {
        var worldData = ZoneTestKit.EmptyWorldData();

        var found = LuckyTicketRewardResolver.TryDraw(worldData, new Random(1), 17124, previousTribe: 0, level1: 50,
            level2: 0, eliteTierEnabled: false, out var rewardItemId);

        Assert.False(found);
        Assert.Equal(0, rewardItemId);
    }

    [Fact]
    public void TryDraw_NoCatalogItemEverMatches_ReturnsFalseAfterExhaustingRetries()
    {
        var worldData = ZoneTestKit.EmptyWorldData();

        var found = LuckyTicketRewardResolver.TryDraw(worldData, new Random(7), 1035, previousTribe: 0, level1: 50,
            level2: 0, eliteTierEnabled: false, out var rewardItemId);

        Assert.False(found);
        Assert.Equal(0, rewardItemId);
    }

    [Fact]
    public void TryDraw_LevelBelow5_AlwaysCommonTier_AndOnlyCommonTierItemsAreEverEligible()
    {
        // level1 < 5 forces the Common tier regardless of the roll (every bracket falls back to Common below
        // level 5) -- and level2 >= 1 collapses the level window to the single fixed value level1+level2,
        // removing the level pick's own randomness too. Every one of the 8 sorts tribe 0's own pool can ever
        // pick (Rare's cape-widening never applies -- tier is always Common here) has a matching Common-tier
        // item at that one fixed level, so the draw always succeeds on its very first attempt regardless of
        // which sort a live, uncontrolled Random happens to pick.
        const int fixedLevel = 1 + 5; // level1=1, level2=5
        var commonSorts = new byte[] { 7, 9, 10, 11, 12, 13, 14, 15 }; // Amulet/Armor/Glove/Ring/Boots/Sword/Blade/Marble
        var itemsById = new Dictionary<int, ItemDefinition>();
        var expectedIds = new HashSet<int>();
        foreach (var sort in commonSorts)
        {
            var itemId = 30000 + sort;
            itemsById[itemId] = new ItemDefinition(
                EligibleItem(itemId, fixedLevel, LuckyTicketRewardResolver.Common, sort), []);
            expectedIds.Add(itemId);
        }

        var worldData = ZoneTestKit.EmptyWorldData(itemsById.ToFrozenDictionary());

        var found = LuckyTicketRewardResolver.TryDraw(worldData, Random.Shared, 1035, previousTribe: 0, level1: 1,
            level2: 5, eliteTierEnabled: false, out var rewardItemId);

        Assert.True(found);
        Assert.Contains(rewardItemId, expectedIds);
    }

    [Fact]
    public void TryDraw_RareTierRoll_WidensThePoolToIncludeCape_AndDrawsTheCapeItem()
    {
        // Scripted sequence: roll=0 (below every ticket's own firstThreshold, forcing bracket 1); level1=50
        // (in [45,100), so bracket 1 resolves to Rare -- the only tier that widens the pool to include Cape);
        // chosenSort index 8 (the 9th/last slot of the 9-element pool -- Amulet/Armor/Glove/Ring/Boots/Sword/
        // Blade/Marble/Cape -- reachable ONLY because includeCape widened the pool to 9 slots at all); level
        // pick index 5 (level1-5+5 = 50, landing on the one seeded item).
        var random = new ScriptedRandom(0, 8, 5);
        var worldData = ZoneTestKit.EmptyWorldData(new Dictionary<int, ItemDefinition>
        {
            [55000] = new(EligibleItem(55000, 50, LuckyTicketRewardResolver.Rare, sort: 8 /* Cape */), [])
        }.ToFrozenDictionary());

        var found = LuckyTicketRewardResolver.TryDraw(worldData, random, 1037, previousTribe: 0, level1: 50,
            level2: 0, eliteTierEnabled: false, out var rewardItemId);

        Assert.True(found);
        Assert.Equal(55000, rewardItemId);
    }

    /// <summary>Deterministic <see cref="Random" /> stand-in identical in shape to <c>GeneralItemDropResolverTests</c>' own.</summary>
    private sealed class ScriptedRandom(params int[] sequence) : Random
    {
        private int _index;

        public override int Next(int maxValue)
        {
            var value = sequence[_index % sequence.Length];
            _index++;
            return maxValue <= 0 ? 0 : value % maxValue;
        }
    }
}
