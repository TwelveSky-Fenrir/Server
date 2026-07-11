using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Covers the optional recognition-serial carry field added to <see cref="DroppedItem" /> (the finding's
///     "add an optional serial to the loot item", once the wire format was confirmed to carry one via
///     <see cref="GroundItemEntity.SerialNumber" />), plus the CP-Gift base-rate wiring this wave enabled by
///     replacing <c>MonsterSpawnScheduler</c>'s hardcoded Zone126-type <c>false</c> with the real
///     <c>Zone.IsZone126TypeZone</c> classification.
/// </summary>
public class DroppedItemSerialTests
{
    [Fact]
    public void DroppedItem_DefaultsSerialToZero()
    {
        // Every existing 2-arg producer (27 call sites) must keep meaning "no explicit serial".
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

    // --- CP-Gift base-rate wiring (the effect of passing zone.IsZone126TypeZone instead of a hardcoded false) ---
    //
    // ScriptedRandom [7, 4, 0] makes LootRandomSource.RandomNumber = (1+7)*(1+4) = 40 (its two Next(0,1000)
    // draws), then, only if the gate proceeds, a card-selection Next(0,100) = 0 (the "5" card, id 691). At the
    // Level2 cap of 12 the CP-Gift base rate is 50 when Zone126-type and 25 otherwise
    // (Server/ts25zone/S07_MyGame05.cpp:3402), and the card drops when the roll is <= the rate. A roll of 40
    // therefore drops on a Zone126-type shard (40 <= 50) but not on an ordinary one (40 > 25) -- exactly the
    // behavior difference this wave's classification wiring turns on.

    private const int CpGiftCard5ItemId = 691;
    private const int Level2Cap = 12;

    [Fact]
    public void ResolveCpGiftCard_OnZone126TypeShard_DropsWhereOrdinaryShardWouldNot()
    {
        var onZone126 = MonsterDropTailResolver.ResolveCpGiftCard(
            generalDropEligible: true, monsterId: 500, isZone241TypeShard: false, killerLevel2: Level2Cap,
            isZone126TypeShard: true, random: new ScriptedRandom(7, 4, 0));

        Assert.Single(onZone126);
        Assert.Equal(CpGiftCard5ItemId, onZone126[0].ItemId);
    }

    [Fact]
    public void ResolveCpGiftCard_OnOrdinaryShard_SameRollYieldsNoDrop()
    {
        var onOrdinary = MonsterDropTailResolver.ResolveCpGiftCard(
            generalDropEligible: true, monsterId: 500, isZone241TypeShard: false, killerLevel2: Level2Cap,
            isZone126TypeShard: false, random: new ScriptedRandom(7, 4, 0));

        Assert.Empty(onOrdinary);
    }

    /// <summary>
    ///     Overrides <see cref="Random.Next(int, int)" /> to return a fixed, repeating sequence -- the same
    ///     shape as <c>LootRandomSourceTests</c>' own private double, so the product-distribution roll and the
    ///     card-selection draw are both deterministic.
    /// </summary>
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
