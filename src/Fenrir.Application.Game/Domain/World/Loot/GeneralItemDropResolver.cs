using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class GeneralItemDropResolver
{
    private const int Amulet = 7;
    private const int Cape = 8;
    private const int Armor = 9;
    private const int Glove = 10;
    private const int Ring = 11;
    private const int Boots = 12;
    private const int Sword = 13;
    private const int Blade = 14;
    private const int Marble = 15;
    private const int Katana = 16;
    private const int DoubleBlade = 17;
    private const int Lute = 18;
    private const int LongBlade = 19;
    private const int Spear = 20;
    private const int Scepter = 21;
    private const int SkillBook = 5;

    private const int
        Pet = 22;

    private const int MaxAttempts = 10;

    public static int? Resolve(WorldDataCache worldData, Random random, byte killerTribe, int itemType,
        int levelLow, int levelHigh, bool includeCape = true, bool includeSkillBook = true)
    {
        var weapon1 = killerTribe switch
        {
            0 => Sword,
            1 => Katana,
            2 => LongBlade,
            _ => -1
        };

        if (weapon1 < 0)
            return null;

        var weapon2 = killerTribe switch
        {
            0 => Blade,
            1 => DoubleBlade,
            2 => Spear,
            _ => -1
        };

        var weapon3 = killerTribe switch
        {
            0 => Marble,
            1 => Lute,
            2 => Scepter,
            _ => -1
        };

        Span<int> pool = stackalloc int[10];
        var poolSize = 0;
        pool[poolSize++] = Amulet;
        pool[poolSize++] = Armor;
        pool[poolSize++] = Glove;
        pool[poolSize++] = Ring;
        pool[poolSize++] = Boots;
        pool[poolSize++] = weapon1;
        pool[poolSize++] = weapon2;
        pool[poolSize++] = weapon3;
        if (includeCape)
            pool[poolSize++] = Cape;
        if (includeSkillBook)
            pool[poolSize++] = SkillBook;

        var chosenSort = pool[random.Next(poolSize)];

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var level = levelLow == levelHigh ? levelLow : levelLow + random.Next(levelHigh - levelLow + 1);

            var candidate = ReturnOne(worldData, random, level, itemType, chosenSort);
            if (candidate is null)
                continue;

            if (candidate.CheckMonsterDrop != 2 || candidate.CheckAvatarTrade != 2 || candidate.CheckSetItem != 1 ||
                candidate.Sort >= Pet)
                continue;

            if (candidate.EquipInfo1 == 1 || candidate.EquipInfo1 - 2 == killerTribe)
                return candidate.ItemId;
        }

        return null;
    }

    private static ItemRowDto? ReturnOne(WorldDataCache worldData, Random random, int level, int type, int sort)
    {
        List<ItemRowDto>? matches = null;
        foreach (var definition in worldData.ItemsById.Values)
        {
            var item = definition.Item;
            if (item.Level != level || item.Type != type || item.Sort != sort)
                continue;

            (matches ??= []).Add(item);
        }

        return matches is null or { Count: 0 } ? null : matches[random.Next(matches.Count)];
    }
}
