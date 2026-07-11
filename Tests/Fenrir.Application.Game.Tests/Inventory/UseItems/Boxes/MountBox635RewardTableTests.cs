using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

public class MountBox635RewardTableTests
{
    private const int Today = 20260710;

    [Fact]
    public void RewardItemIds_AreExactlyTheEightCitedAnimalIds_InCitationOrder()
    {
        int[] expected = [1307, 1308, 1309, 1315, 1319, 1322, 1325, 1328];
        Assert.Equal(expected, MountBox635RewardTable.RewardItemIds.ToArray());
    }

    [Fact]
    public void RewardItemIds_ExcludesPuma3_1331()
    {
        Assert.DoesNotContain(1331, MountBox635RewardTable.RewardItemIds);
    }

    [Fact]
    public void RewardItemIds_HasNoDuplicates()
    {
        Assert.Equal(MountBox635RewardTable.RewardItemIds.Length,
            MountBox635RewardTable.RewardItemIds.Distinct().Count());
    }

    [Fact]
    public void CreateSpec_IsUniform_BoxId635_NoRental()
    {
        var spec = MountBox635RewardTable.CreateSpec();

        Assert.Equal(MountBox635RewardTable.BoxItemId, spec.BoxId);
        Assert.Equal(635, spec.BoxId);
        Assert.Equal(BoxRewardKind.Uniform, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(MountBox635RewardTable.RewardItemIds.ToArray(), spec.UniformIds.ToArray());
    }

    [Theory]
    [InlineData(0, 1307)]
    [InlineData(1, 1308)]
    [InlineData(2, 1309)]
    [InlineData(3, 1315)]
    [InlineData(4, 1319)]
    [InlineData(5, 1322)]
    [InlineData(6, 1325)]
    [InlineData(7, 1328)]
    public void RollRewardId_UniformDraw_MapsIndexToExpectedAnimal(int drawIndex, int expectedItemId)
    {
        var spec = MountBox635RewardTable.CreateSpec();

        var rolled = spec.RollRewardId(new ScriptedRandom(drawIndex));

        Assert.Equal(expectedItemId, rolled);
    }

    [Fact]
    public void OpenSingle_ThroughTheRealMechanism_PlacesOneOfTheEightRewards_AndConsumesExactlyOneBox()
    {
        var spec = MountBox635RewardTable.CreateSpec();
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(635, 3));

        var plan = LootBoxOpenResolver.OpenSingle(spec, 0, 0, Box(635, 3), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((1315, 4)), new ScriptedRandom(3), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(1315, plan.RewardItemId);
        Assert.Equal(2, plan.BoxRemainingQuantity);
        Assert.Equal(1315, plan.ProjectedPage0[1].ItemId);
        Assert.Equal(2, plan.ProjectedPage0[0].Quantity);
    }

    [Fact]
    public void OpenSingle_LastBoxUnit_ClearsTheSourceSlot()
    {
        var spec = MountBox635RewardTable.CreateSpec();
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(635, 1));

        var plan = LootBoxOpenResolver.OpenSingle(spec, 0, 0, Box(635, 1), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((1307, 4)), new ScriptedRandom(0), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(0, plan.BoxRemainingQuantity);
        Assert.False(plan.ProjectedPage0.ContainsKey(0));
        Assert.Equal(1307, plan.ProjectedPage0[1].ItemId);
    }

    [Fact]
    public void BoxItemId_IsNotInBulkOpenWhitelist_SoAnyRequestedOpenCountIsIgnored()
    {
        Assert.DoesNotContain(MountBox635RewardTable.BoxItemId, (IEnumerable<int>)LootBoxCatalog.BulkOpenWhitelist);
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
