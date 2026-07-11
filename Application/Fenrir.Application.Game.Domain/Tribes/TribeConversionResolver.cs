using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Tribes;

public sealed class TribeConversionResolver
{
    public const byte NobleDragon = 0;
    public const byte RoyalSerpent = 1;
    public const byte GrandTiger = 2;
    public const byte Neutral = 3;

    public const int BookNobleDragon = 99014;
    public const int BookRoyalSerpent = 99015;
    public const int BookGrandTiger = 99016;

    public const int ScrollA = 8153;
    public const int ScrollB = 8154;

    private const int V2BandLow = 87129;
    private const int V2BandHigh = 87257;
    private const int V2Offset = 129;

    private readonly FrozenDictionary<(byte Tribe, int ItemId), byte> _costumeGroupBySourceItem;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _costumeIdByGroupTribe;
    private readonly FrozenDictionary<(byte Tribe, int ItemId), byte> _itemGroupBySourceItem;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _itemIdByGroupTribe;
    private readonly FrozenDictionary<(byte Tribe, int SkillId), byte> _skillGroupBySourceSkill;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _skillIdByGroupTribe;

    public TribeConversionResolver(
        IReadOnlyList<TribeSkillEquivalenceRowDto> skillEquivalences,
        IReadOnlyList<TribeItemEquivalenceRowDto> itemEquivalences,
        IReadOnlyList<TribeCostumeEquivalenceRowDto> costumeEquivalences)
    {
        ArgumentNullException.ThrowIfNull(skillEquivalences);
        ArgumentNullException.ThrowIfNull(itemEquivalences);
        ArgumentNullException.ThrowIfNull(costumeEquivalences);

        var skillGroup = new Dictionary<(byte, int), byte>(skillEquivalences.Count);
        var skillById = new Dictionary<(byte, byte), int>(skillEquivalences.Count);
        foreach (var row in skillEquivalences)
        {
            skillGroup[(row.TribeId, row.SkillId)] = row.GroupIndex;
            skillById[(row.GroupIndex, row.TribeId)] = row.SkillId;
        }

        var itemGroup = new Dictionary<(byte, int), byte>(itemEquivalences.Count);
        var itemById = new Dictionary<(byte, byte), int>(itemEquivalences.Count);
        foreach (var row in itemEquivalences)
        {
            itemGroup[(row.TribeId, row.ItemId)] = row.GroupIndex;
            itemById[(row.GroupIndex, row.TribeId)] = row.ItemId;
        }

        var costumeGroup = new Dictionary<(byte, int), byte>(costumeEquivalences.Count);
        var costumeById = new Dictionary<(byte, byte), int>(costumeEquivalences.Count);
        foreach (var row in costumeEquivalences)
        {
            costumeGroup[(row.TribeId, row.ItemId)] = row.GroupIndex;
            costumeById[(row.GroupIndex, row.TribeId)] = row.ItemId;
        }

        _skillGroupBySourceSkill = skillGroup.ToFrozenDictionary();
        _skillIdByGroupTribe = skillById.ToFrozenDictionary();
        _itemGroupBySourceItem = itemGroup.ToFrozenDictionary();
        _itemIdByGroupTribe = itemById.ToFrozenDictionary();
        _costumeGroupBySourceItem = costumeGroup.ToFrozenDictionary();
        _costumeIdByGroupTribe = costumeById.ToFrozenDictionary();
    }

        public static bool IsPlayableTribe(byte tribe)
    {
        return tribe <= GrandTiger;
    }

        public bool TryGetBookTargetTribe(int itemId, out byte toTribe)
    {
        switch (itemId)
        {
            case BookNobleDragon:
                toTribe = NobleDragon;
                return true;
            case BookRoyalSerpent:
                toTribe = RoyalSerpent;
                return true;
            case BookGrandTiger:
                toTribe = GrandTiger;
                return true;
            default:
                toTribe = 0;
                return false;
        }
    }

        public bool TryRemapItem(byte fromTribe, byte toTribe, int itemId, out int newItemId)
    {
        newItemId = itemId;

        var isV2 = itemId is >= V2BandLow and <= V2BandHigh;
        var baseId = isV2 ? itemId - V2Offset : itemId;

        if (!_itemGroupBySourceItem.TryGetValue((fromTribe, baseId), out var group))
            return false;
        if (!_itemIdByGroupTribe.TryGetValue((group, toTribe), out var targetBase))
            return false;

        newItemId = isV2 ? targetBase + V2Offset : targetBase;
        return true;
    }

        public bool TryRemapSkill(byte fromTribe, byte toTribe, int skillId, out int newSkillId)
    {
        newSkillId = skillId;

        if (!_skillGroupBySourceSkill.TryGetValue((fromTribe, skillId), out var group))
            return false;
        if (!_skillIdByGroupTribe.TryGetValue((group, toTribe), out var targetSkill))
            return false;

        newSkillId = targetSkill;
        return true;
    }

        public bool TryRemapCostume(byte fromTribe, byte toTribe, int itemId, out int newItemId)
    {
        newItemId = itemId;

        if (!_costumeGroupBySourceItem.TryGetValue((fromTribe, itemId), out var group))
            return false;
        if (!_costumeIdByGroupTribe.TryGetValue((group, toTribe), out var targetItem))
            return false;

        newItemId = targetItem;
        return true;
    }

        public bool AreAllItemsMappable(byte fromTribe, byte toTribe, IReadOnlyList<int> equippedItemIds)
    {
        ArgumentNullException.ThrowIfNull(equippedItemIds);

        for (var i = 0; i < equippedItemIds.Count; i++)
            if (!TryRemapItem(fromTribe, toTribe, equippedItemIds[i], out _))
                return false;

        return true;
    }
}
