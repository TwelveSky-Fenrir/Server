using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetSlotAmuletBonusTable
{
    public const byte RequiredSortCode = 28;

    public static readonly ImmutableHashSet<int> QualifyingItemIds = BuildQualifyingIds();

    private static readonly FrozenDictionary<int, (float Life, float Mana)> ConfirmedBonuses =
        new Dictionary<int, (float Life, float Mana)>
        {
            [76000] = (3000f, 3000f),
            [76001] = (3000f, 3000f),
            [76002] = (3000f, 3000f),
            [76003] = (3000f, 3000f),
            [76004] = (3000f, 3000f),
            [76005] = (5000f, 5000f),
            [76006] = (7500f, 7500f),
            [76007] = (12500f, 12500f),
            [8290] = (550f, 500f)
        }.ToFrozenDictionary();

    public static (float Life, float Mana) GetBaseBonus(int itemId, FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (!itemsById.TryGetValue(itemId, out var definition) || definition.Item.Sort != RequiredSortCode)
            return (0f, 0f);

        return ConfirmedBonuses.TryGetValue(itemId, out var bonus) ? bonus : (0f, 0f);
    }

    private static ImmutableHashSet<int> BuildQualifyingIds()
    {
        var builder = ImmutableHashSet.CreateBuilder<int>();
        AddRange(builder, 2151, 2154);
        AddRange(builder, 2174, 2189);
        AddRange(builder, 2195, 2206);
        AddRange(builder, 2253, 2254);
        AddRange(builder, 2261, 2262);
        AddRange(builder, 2301, 2302);
        AddRange(builder, 2410, 2421);
        builder.Add(8290);
        AddRange(builder, 76000, 76007);
        return builder.ToImmutable();
    }

    private static void AddRange(ImmutableHashSet<int>.Builder builder, int lowInclusive, int highInclusive)
    {
        for (var id = lowInclusive; id <= highInclusive; id++) builder.Add(id);
    }
}
