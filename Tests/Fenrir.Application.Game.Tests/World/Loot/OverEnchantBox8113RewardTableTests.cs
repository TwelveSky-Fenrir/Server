using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class OverEnchantBox8113RewardTableTests
{
    private const int Today = 20260710;

    [Fact]
    public void RewardItemIds_AreExactlyTheNineCitedIds_InCitationOrder()
    {
        int[] expected = [8101, 8102, 8106, 1103, 1126, 1166, 1222, 1237, 828];
        Assert.Equal(expected, OverEnchantBox8113RewardTable.RewardItemIds.ToArray());
    }

    [Fact]
    public void RewardItemIds_HasNoDuplicates()
    {
        Assert.Equal(OverEnchantBox8113RewardTable.RewardItemIds.Length,
            OverEnchantBox8113RewardTable.RewardItemIds.Distinct().Count());
    }

    [Fact]
    public void Spec_IsUniform_BoxId8113_NoRental()
    {
        var spec = OverEnchantBox8113RewardTable.Spec;

        Assert.Equal(OverEnchantBox8113RewardTable.BoxItemId, spec.BoxId);
        Assert.Equal(8113, spec.BoxId);
        Assert.Equal(BoxRewardKind.Uniform, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(OverEnchantBox8113RewardTable.RewardItemIds.ToArray(), spec.UniformIds.ToArray());
    }

    [Fact]
    public void BoxItemId_IsAlreadyInBulkOpenWhitelist()
    {
        Assert.Contains(OverEnchantBox8113RewardTable.BoxItemId, LootBoxCatalog.BulkOpenWhitelist);
    }

    [Theory]
    [InlineData(0, 8101)]
    [InlineData(1, 8102)]
    [InlineData(2, 8106)]
    [InlineData(3, 1103)]
    [InlineData(4, 1126)]
    [InlineData(5, 1166)]
    [InlineData(6, 1222)]
    [InlineData(7, 1237)]
    [InlineData(8, 828)]
    public void RollRewardId_UniformDraw_MapsIndexToExpectedReward(int drawIndex, int expectedItemId)
    {
        var rolled = OverEnchantBox8113RewardTable.Spec.RollRewardId(new ScriptedRandom(drawIndex));

        Assert.Equal(expectedItemId, rolled);
    }

    [Fact]
    public void OpenSingle_ThroughTheRealMechanism_PlacesOneOfTheNineRewards_AndConsumesExactlyOneBox()
    {
        var spec = OverEnchantBox8113RewardTable.Spec;
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(8113, 3));

        var plan = LootBoxOpenResolver.OpenSingle(spec, 0, 0, Box(8113, 3), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((1126, 4)), new ScriptedRandom(4), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(1126, plan.RewardItemId);
        Assert.Equal(2, plan.BoxRemainingQuantity);
        Assert.Equal(1126, plan.ProjectedPage0[1].ItemId);
        Assert.Equal(2, plan.ProjectedPage0[0].Quantity);
    }

    [Fact]
    public void OpenSingle_LastBoxUnit_ClearsTheSourceSlot()
    {
        var spec = OverEnchantBox8113RewardTable.Spec;
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(8113, 1));

        var plan = LootBoxOpenResolver.OpenSingle(spec, 0, 0, Box(8113, 1), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((8101, 4)), new ScriptedRandom(0), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(0, plan.BoxRemainingQuantity);
        Assert.False(plan.ProjectedPage0.ContainsKey(0));
        Assert.Equal(8101, plan.ProjectedPage0[1].ItemId);
    }

    private static ItemStack Box(int id, int quantity)
    {
        return new ItemStack(id, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static Func<int, byte?> Sorts(params (int Id, int Sort)[] rows)
    {
        var map = rows.ToDictionary(r => r.Id, r => (byte)r.Sort);
        return id => map.TryGetValue(id, out var sort) ? sort : null;
    }

        private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException(
                    "ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
