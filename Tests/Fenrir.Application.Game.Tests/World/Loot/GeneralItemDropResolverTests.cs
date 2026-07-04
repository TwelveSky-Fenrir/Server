using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class GeneralItemDropResolverTests
{
    private const int Common = 1;
    private const int Armor = 9;
    private const int Sword = 13; // tribe 0's own weapon sort

    private static WorldDataCache CacheWith(params ItemRowDto[] items)
    {
        var rows = WorldDataTestRows.MinimalRows() with { Items = items };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static ItemRowDto EligibleItem(int itemId, int level, byte type, byte sort, byte equipInfo1 = 1)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Level = (short)level,
            Type = type,
            Sort = sort,
            CheckMonsterDrop = 2,
            CheckAvatarTrade = 2,
            CheckSetItem = 1,
            EquipInfo1 = equipInfo1
        };
    }

    [Fact]
    public void Resolve_MatchingEligibleItem_IsReturned()
    {
        var item = EligibleItem(9001, 10, Common, Armor);
        var cache = CacheWith(item);
        // sortPool[1] == Armor -- see class remarks on the pool's fixed order.
        var random = new ScriptedRandom(1);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Equal(9001, result);
    }

    [Fact]
    public void Resolve_ItemNotFlaggedAsMonsterDroppable_IsNeverReturned()
    {
        var item = EligibleItem(9002, 10, Common, Armor) with { CheckMonsterDrop = 1 };
        var cache = CacheWith(item);
        var random = new ScriptedRandom(1);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_TribeRestrictedItem_RejectedForTheWrongTribe()
    {
        // EquipInfo1 == 3 means "tribe 1 only" (EquipInfo1 - 2 == tribe).
        var item = EligibleItem(9003, 10, Common, Sword, 3);
        var cache = CacheWith(item);
        // sortPool[5] is tribe 0's own weapon sort (Sword) -- picked, but the item itself is tribe-1-restricted.
        var random = new ScriptedRandom(5);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_TribeRestrictedItem_AcceptedForTheRightTribe()
    {
        // EquipInfo1 == 2 means "tribe 0 only" (EquipInfo1 - 2 == tribe), matching sortPool[5] == Sword for tribe 0.
        var item = EligibleItem(9004, 10, Common, Sword, 2);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(5);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Equal(9004, result);
    }

    [Fact]
    public void Resolve_NoCandidateAtAll_ReturnsNullWithinAttemptBudget()
    {
        // A non-empty catalog (WorldDataCacheBuilder.Build requires world.Items non-empty) whose one item
        // simply never matches the (level,type,sort) triple being resolved for.
        var cache = CacheWith(EligibleItem(9099, 99, Common, Armor));
        var random = new ScriptedRandom(0);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_UnknownTribe_ReturnsNull()
    {
        var item = EligibleItem(9005, 10, Common, Armor);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(1);

        var result = GeneralItemDropResolver.Resolve(cache, random, 9, Common, 10,
            10);

        Assert.Null(result);
    }

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
