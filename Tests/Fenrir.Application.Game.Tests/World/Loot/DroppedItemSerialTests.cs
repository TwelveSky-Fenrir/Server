using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class DroppedItemSerialTests
{
    private const int CpGiftCard5ItemId = 691;
    private const int Level2Cap = 12;

    [Fact]
    public void DroppedItem_DefaultsSerialToZero()
    {
        var item = new DroppedItem(691, 1);

        Assert.Equal(0, item.Serial);
    }

    [Fact]
    public void DroppedItem_CarriesExplicitSerial()
    {
        var item = new DroppedItem(1444, 1, 12345);

        Assert.Equal(1444, item.ItemId);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(12345, item.Serial);
    }

    [Fact]
    public void DroppedItem_SerialParticipatesInValueEquality()
    {
        var withoutSerial = new DroppedItem(1444, 1);
        var alsoWithoutSerial = new DroppedItem(1444, 1);
        var withSerial = new DroppedItem(1444, 1, 777);

        Assert.Equal(withoutSerial, alsoWithoutSerial);
        Assert.NotEqual(withoutSerial, withSerial);
    }

    [Fact]
    public void ResolveCpGiftCard_OnZone126TypeShard_DropsWhereOrdinaryShardWouldNot()
    {
        var onZone126 = MonsterDropTailResolver.ResolveCpGiftCard(
            true, 500, false, Level2Cap,
            true, new ScriptedRandom(7, 4, 0));

        Assert.Single(onZone126);
        Assert.Equal(CpGiftCard5ItemId, onZone126[0].ItemId);
    }

    [Fact]
    public void ResolveCpGiftCard_OnOrdinaryShard_SameRollYieldsNoDrop()
    {
        var onOrdinary = MonsterDropTailResolver.ResolveCpGiftCard(
            true, 500, false, Level2Cap,
            false, new ScriptedRandom(7, 4, 0));

        Assert.Empty(onOrdinary);
    }

    private sealed class ScriptedRandom(params int[] sequence) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            var value = sequence[_index % sequence.Length];
            _index++;
            return minValue + value % (maxValue - minValue);
        }
    }
}
