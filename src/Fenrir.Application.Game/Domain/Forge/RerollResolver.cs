using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Forge;

public static class RerollResolver
{
    public enum RerollOutcome
    {
        Rejected,
        NoCandidate,
        Success
    }

    public const byte RareItemType = 3;
    public const byte EliteItemType = 4;
    private const int MaxBaseLevel = 145;
    private const int MaxMartialLevel = 12;

    private const int Mg5OriginRareTopPrice = 250_000_000;

    private const int Mg5OriginEliteTopPrice = 500_000_000;

    private const byte IAmulet = 7;
    private const byte ICape = 8;
    private const byte IArmor = 9;
    private const byte IGlove = 10;
    private const byte IRing = 11;
    private const byte IBoots = 12;
    private const byte ISword = 13;
    private const byte IBlade = 14;
    private const byte IMarble = 15;
    private const byte IKatana = 16;
    private const byte IDblade = 17;
    private const byte ILute = 18;
    private const byte ILblade = 19;
    private const byte ISpear = 20;
    private const byte IScepter = 21;

    private static readonly short[] RareLevelTiers =
        [45, 55, 65, 75, 85, 95, 105, 114, 117, 120, 123, 126, 129, 132, 135, 138, 141, 144];

    private static readonly short[] EliteLevelTiers =
        [100, 110, 113, 115, 118, 121, 124, 127, 130, 133, 136, 139, 142];

    public static RerollResult Resolve(
        ItemRowDto targetItem, byte previousTribe,
        IEnumerable<ItemRowDto> catalog, IRandomSource random)
    {
        if (targetItem.Type is not (RareItemType or EliteItemType) || targetItem.Sort is < 7 or > 21 ||
            targetItem.CheckExchange != 2)
            return new RerollResult(RerollOutcome.Rejected, 0, 0);

        var cost = GetExchangeMoney(targetItem);

        var categories = BuildCategoryList(targetItem.Type, previousTribe);
        var category = categories[random.NextInt32(categories.Length)];
        var combinedLevel = targetItem.Level + targetItem.MartialLevel;

        var replacement = FindReplacement(targetItem, combinedLevel, category, catalog, random);

        return replacement != null
            ? new RerollResult(RerollOutcome.Success, cost, replacement.ItemId)
            : new RerollResult(RerollOutcome.NoCandidate, cost, 0);
    }

    private static byte[] BuildCategoryList(byte type, byte previousTribe)
    {
        var (w1, w2, w3) = previousTribe switch
        {
            0 => (ISword, IBlade, IMarble),
            1 => (IKatana, IDblade, ILute),
            2 => (ILblade, ISpear, IScepter),
            _ => ((byte)0, (byte)0, (byte)0)
        };

        return type == RareItemType
            ? [IAmulet, ICape, IArmor, IGlove, IRing, IBoots, w1, w2, w3]
            : [IAmulet, IArmor, IGlove, IRing, IBoots, w1, w2, w3];
    }

    private static ItemRowDto? FindReplacement(ItemRowDto target, int combinedLevel, byte category,
        IEnumerable<ItemRowDto> catalog, IRandomSource random)
    {
        if (category is < 1 or > 22)
            return null;

        List<ItemRowDto> candidates = [];

        if (combinedLevel <= MaxBaseLevel)
        {
            foreach (var candidate in catalog)
            {
                if (candidate.Level != combinedLevel || candidate.MartialLevel != 0)
                    continue;
                if (candidate.Type != target.Type || candidate.Sort != category)
                    continue;
                if (candidate.EquipInfo1 != target.EquipInfo1)
                    continue;
                if (candidate.CheckExchange != target.CheckExchange)
                    continue;
                if (candidate.CheckAvatarTrade != target.CheckAvatarTrade)
                    continue;

                candidates.Add(candidate);
            }
        }
        else
        {
            var martialLevel = combinedLevel - MaxBaseLevel;

            foreach (var candidate in catalog)
            {
                if (candidate.Level != MaxBaseLevel || candidate.MartialLevel != martialLevel)
                    continue;
                if (candidate.Type != target.Type || candidate.Sort != category)
                    continue;
                if (candidate.EquipInfo1 != target.EquipInfo1)
                    continue;
                if (candidate.ItemId == target.ItemId)
                    continue;
                if (candidate.CheckExchange != target.CheckExchange)
                    continue;
                if (candidate.CheckAvatarTrade != target.CheckAvatarTrade)
                    continue;
                if (candidate.CheckHighItem != target.CheckHighItem)
                    continue;
                if (candidate.CheckLowItem != target.CheckLowItem)
                    continue;
                if (candidate.CheckImprove != target.CheckImprove)
                    continue;
                if (candidate.CheckHighImprove != target.CheckHighImprove)
                    continue;
                if (candidate.CheckSetItem == 2 && candidate.CheckSetItem != target.CheckSetItem)
                    continue;
                if (target.CheckSetItem == 1 && candidate.CheckSetItem > 2)
                    continue;

                candidates.Add(candidate);
            }
        }

        return candidates.Count == 0 ? null : candidates[random.NextInt32(candidates.Count)];
    }

    private static int GetExchangeMoney(ItemRowDto item)
    {
        var isRare = item.Type == RareItemType;

        if (item.Level == MaxBaseLevel && item.MartialLevel == MaxMartialLevel && item.CheckSetItem > 1)
            return isRare ? Mg5OriginRareTopPrice : Mg5OriginEliteTopPrice;

        var tiers = isRare ? RareLevelTiers : EliteLevelTiers;
        var baseMoney = isRare ? 1_000_000 : 4_000_000;
        var increment = isRare ? 200_000 : 400_000;

        var tierIndex = Array.IndexOf(tiers, item.Level);
        if (tierIndex >= 0) return baseMoney + increment * tierIndex;
        if (item.Level != MaxBaseLevel || item.MartialLevel is > 12)
            return 0;

        tierIndex = tiers.Length + item.MartialLevel;

        return baseMoney + increment * tierIndex;
    }

    public readonly record struct RerollResult(RerollOutcome Outcome, int Cost, int ResultItemId);
}
