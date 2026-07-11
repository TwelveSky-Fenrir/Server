using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class GeneralItemDropResolverTests
{
    private const int Common = 1;
    private const int Armor = 9;
    private const int Sword = 13;


    private const int Cape = 8;
    private const int SkillBook = 5;

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
        var item = EligibleItem(9003, 10, Common, Sword, 3);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(5);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10,
            10);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_TribeRestrictedItem_AcceptedForTheRightTribe()
    {
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

    [Fact]
    public void Resolve_IncludeCapeDefaultTrue_CapeSlotIsReachable()
    {
        var item = EligibleItem(9006, 10, Common, Cape);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(8);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10, 10);

        Assert.Equal(9006, result);
    }

    [Fact]
    public void Resolve_IncludeCapeFalse_CapeSlotIsNeverReachable_EvenIfThatIndexWouldOtherwiseHitIt()
    {
        var item = EligibleItem(9007, 10, Common, Cape);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(0, 1, 2, 3, 4, 5, 6, 7);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10, 10,
            false, false);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_IncludeSkillBookFalse_SkillBookSlotIsNeverReachable()
    {
        var item = EligibleItem(9008, 10, Common, SkillBook);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(0, 1, 2, 3, 4, 5, 6, 7, 8);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10, 10,
            true, false);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_IncludeCapeAndSkillBookBothFalse_PoolIsExactlyTheEightFixedSlots()
    {
        var item = EligibleItem(9009, 10, Common, Armor);
        var cache = CacheWith(item);
        var random = new ScriptedRandom(1);

        var result = GeneralItemDropResolver.Resolve(cache, random, 0, Common, 10, 10,
            false, false);

        Assert.Equal(9009, result);
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
