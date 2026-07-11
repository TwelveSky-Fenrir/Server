using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Forge;

/// <summary>
///     Pure resolver for CZ_HIGH_ITEM_SEND (S04_MyWork02.cpp:3992, upgrade) and CZ_LOW_ITEM_SEND
///     (S04_MyWork02.cpp:4136, downgrade). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     <c>USE_ULTIMATE_UPGRADE_STONE</c> (material 984) is CONFIRMED dead in the shipped ReleaseEU33 build: it
///     is defined only in the never-compiled <c>#else</c> of <c>#ifdef M33</c> (Server/Header/Protocol/DEFINE.h:40),
///     so the material check accepts only 1024/1025, the +10 probability bonus never applies, and the
///     "IS -4 / IU -1" success formula always holds -- reproduced exactly, with material 984 simply rejected.
///     The <c>__REBIRTH__</c> Warlord reroll (only reachable at the absolute top tier, encoded level 157, 3/4 of
///     successes) has its cited DECISION modeled -- see <see cref="RankChangeResult.WarlordRerollEligible" /> --
///     but its replacement DRAW is deferred: it needs <c>ReturnWarlordForUpgrade</c>/a per-tribe Warlord item
///     catalog Fenrir doesn't have, and the contract flags a cross-call bonus flag whose reset is unconfirmed
///     (S07_MyGame03.cpp:5425-5433). It degrades gracefully: the plain (non-Warlord) result item is granted.
///     <c>aHighItemValue</c> (the "lucky upgrade" charge)
///     has no acquisition path yet, same open issue as <see cref="CombineResolver" />'s <c>luckyComboCharges</c>.
///     Unlike <c>luckyComboCharges</c>, the lucky-upgrade charge does NOT add a probability bonus in the source
///     (S04_MyWork02.cpp:4051-4078, 4202-4230) -- it is consumed and reported unconditionally, but the roll
///     probability itself is untouched. <see cref="RankChangeResult.ConsumesLuckyCharge" /> still reflects
///     consumption for once an acquisition path exists.
/// </remarks>
public static class RankChangeResolver
{
    public enum RankChangeOutcome
    {
        Rejected,
        NoCandidate,
        Success,
        Failed
    }

    public const byte RareItemType = 3;
    public const byte EliteItemType = 4;
    private const int MaxBaseLevel = 145;
    private const int MaxMartialLevel = 12;

    /// <summary>
    ///     The absolute top forged tier, encoded as base-level 145 + martial 12 = 157. A successful upgrade that
    ///     lands here is the only one that can trigger the <c>__REBIRTH__</c> Warlord reroll -- see
    ///     <see cref="RankChangeResult.WarlordRerollEligible" />.
    /// </summary>
    private const int WarlordRerollEncodedLevel = MaxBaseLevel + MaxMartialLevel;

    /// <summary>Cape slot-type (ICape). A cape is excluded from the Warlord reroll.</summary>
    private const byte CapeSort = 8;

    private const int RareUp145P1024 = 10;
    private const int RareUp145POther = 20;

    private const int EliteUp145P1024 = 2;
    private const int EliteUp145POther = 4;

    private const int RareDown145Martial0P1024 = 1;
    private const int RareDown145Martial0POther = 6;
    private const int RareDown145P1024 = 1;
    private const int RareDown145POther = 2;

    private const int EliteDown145Martial0P1024 = 1;
    private const int EliteDown145Martial0POther = 6;
    private const int EliteDown145P1024 = 1;
    private const int EliteDown145POther = 2;

    private static readonly (short From, int To, int P1024, int POther)[] RareUpTiers =
    [
        (45, 55, 520, 577), (55, 65, 490, 540), (65, 75, 460, 510), (75, 85, 430, 480),
        (85, 95, 400, 450), (95, 105, 370, 420), (105, 114, 340, 390), (114, 117, 310, 360),
        (117, 120, 280, 330), (120, 123, 250, 300), (123, 126, 220, 270), (126, 129, 190, 240),
        (129, 132, 160, 210), (132, 135, 130, 180), (135, 138, 100, 150), (138, 141, 70, 120),
        (141, 144, 40, 90), (144, 145, 10, 60)
    ];

    private static readonly (short From, int To, int P1024, int POther)[] EliteUpTiers =
    [
        (100, 110, 340, 390), (110, 115, 310, 360), (115, 118, 280, 330), (118, 121, 250, 300),
        (121, 124, 220, 270), (124, 127, 190, 240), (127, 130, 160, 210), (130, 133, 130, 180),
        (133, 136, 100, 150), (136, 139, 70, 120), (139, 142, 40, 90), (142, 145, 10, 60)
    ];

    private static readonly (short From, int To, int P1024, int POther)[] RareDownTiers =
    [
        (55, 45, 52, 57), (65, 55, 49, 54), (75, 65, 46, 51), (85, 75, 43, 48),
        (95, 85, 40, 45), (105, 95, 37, 42), (114, 105, 34, 39), (117, 114, 31, 36),
        (120, 117, 28, 33), (123, 120, 25, 30), (126, 123, 22, 27), (129, 126, 19, 24),
        (132, 129, 16, 21), (135, 132, 13, 18), (138, 135, 10, 15), (141, 138, 7, 12),
        (144, 141, 4, 9)
    ];

    private static readonly (short From, int To, int P1024, int POther)[] EliteDownTiers =
    [
        (110, 100, 34, 39), (115, 110, 31, 36), (118, 115, 28, 33), (121, 118, 25, 30),
        (124, 121, 22, 27), (127, 124, 19, 24), (130, 127, 16, 21), (133, 130, 13, 18),
        (136, 133, 10, 15), (139, 136, 7, 12), (142, 139, 4, 9)
    ];

    private static readonly short[] RareHighMoneyLevels =
        [45, 55, 65, 75, 85, 95, 105, 114, 117, 120, 123, 126, 129, 132, 135, 138, 141, 144];

    private static readonly short[] EliteHighMoneyLevels = [100, 110, 115, 118, 121, 124, 127, 130, 133, 136, 139, 142];

    private static readonly short[] RareLowMoneyLevels =
        [55, 65, 75, 85, 95, 105, 114, 117, 120, 123, 126, 129, 132, 135, 138, 141, 144];

    private static readonly short[] EliteLowMoneyLevels = [110, 115, 118, 121, 124, 127, 130, 133, 136, 139, 142];

    public static RankChangeResult ResolveUpgrade(
        ItemDefinition targetDefinition, ItemStack targetStack,
        ItemRowDto materialItem,
        int luck, int luckyUpgradeCharges,
        IEnumerable<ItemDefinition> catalog, IRandomSource random)
    {
        var target = targetDefinition.Item;

        if (target.Type is not (RareItemType or EliteItemType) || target.Sort is < 7 or > 29 ||
            target.CheckHighItem != 2 || targetStack.Enchant < 4 || targetStack.Combine < 1 ||
            target.MartialLevel >= MaxMartialLevel || materialItem.ItemId is not (1024 or 1025))
            return Rejected();

        var (encodedLevel, probability) = ResolveUpTier(target, materialItem.ItemId);

        var cost = GetTieredMoney(
            target.Type == RareItemType ? RareHighMoneyLevels : EliteHighMoneyLevels,
            target.Type == RareItemType ? 100_000 : 1_000_000,
            MaxMartialLevel - 1, target.Level, target.MartialLevel);

        var replacement = FindReplacement(targetDefinition, encodedLevel, true, catalog, random);
        if (replacement is null)
            return new RankChangeResult(RankChangeOutcome.NoCandidate, cost, 0, 0, 0, false);

        var luckyChargeConsumed = luckyUpgradeCharges > 0;

        probability += (int)(luck / 300.0f);

        var success = random.NextInt32(1000) < probability;
        if (!success)
            return new RankChangeResult(RankChangeOutcome.Failed, cost, 0, targetStack.Enchant, targetStack.Combine,
                luckyChargeConsumed);

        // __REBIRTH__ Warlord reroll DECISION (Server/ts25zone/S04_MyWork02.cpp:4082-4102): only when the drawn
        // result is at the absolute top tier (encoded 157) and is not a cape does a 75% chance (a modulo-4 draw
        // being non-zero) elect to reroll into the previous-tribe Warlord set. This resolver models only that
        // cited, unambiguous DECISION -- the Warlord replacement DRAW itself (ReturnWarlordForUpgrade + a
        // per-tribe Warlord catalog Fenrir has no equivalent for, plus a contract-flagged cross-call bonus flag
        // whose reset is unconfirmed) is deferred, so the plain result item is still granted. The rand%4 draw is
        // taken ONLY in this exact top-tier/non-cape condition, so it never perturbs any other path's draw order.
        var warlordEligible = false;
        if (encodedLevel == WarlordRerollEncodedLevel && target.Sort != CapeSort)
            warlordEligible = random.NextInt32(4) != 0;

        return new RankChangeResult(RankChangeOutcome.Success, cost, replacement.Item.ItemId,
            targetStack.Enchant - 4, targetStack.Combine - 1, luckyChargeConsumed, warlordEligible);
    }

    public static RankChangeResult ResolveDowngrade(
        ItemDefinition targetDefinition, ItemStack targetStack,
        ItemRowDto materialItem,
        int luck, int luckyUpgradeCharges,
        IEnumerable<ItemDefinition> catalog, IRandomSource random)
    {
        var target = targetDefinition.Item;

        if (target.Type is not (RareItemType or EliteItemType) || target.Sort is < 7 or > 29 ||
            target.CheckLowItem != 2 || (target.Type == RareItemType && target.Level <= 45) ||
            (target.Type == EliteItemType && target.Level <= 100) || materialItem.ItemId is not (1024 or 1025))
            return Rejected();

        var (encodedLevel, probability) = ResolveDownTier(target, materialItem.ItemId);

        var cost = GetTieredMoney(
            target.Type == RareItemType ? RareLowMoneyLevels : EliteLowMoneyLevels,
            target.Type == RareItemType ? 100_000 : 1_000_000,
            MaxMartialLevel, target.Level, target.MartialLevel);

        var replacement = FindReplacement(targetDefinition, encodedLevel, false, catalog, random);
        if (replacement is null)
            return new RankChangeResult(RankChangeOutcome.NoCandidate, cost, 0, 0, 0, false);

        var luckyChargeConsumed = luckyUpgradeCharges > 0;

        probability += (int)(luck / 300.0f);

        var success = random.NextInt32(100) < probability;
        if (!success)
            return new RankChangeResult(RankChangeOutcome.Failed, cost, 0, targetStack.Enchant, targetStack.Combine,
                luckyChargeConsumed);

        return new RankChangeResult(RankChangeOutcome.Success, cost, replacement.Item.ItemId,
            targetStack.Enchant, targetStack.Combine, luckyChargeConsumed);
    }

    private static RankChangeResult Rejected()
    {
        return new RankChangeResult(RankChangeOutcome.Rejected, 0, 0, 0, 0, false);
    }

    private static (int EncodedLevel, int Probability) ResolveUpTier(ItemRowDto target, int materialItemId)
    {
        var tiers = target.Type == RareItemType ? RareUpTiers : EliteUpTiers;

        foreach (var tier in tiers)
            if (tier.From == target.Level)
                return (tier.To, materialItemId == 1024 ? tier.P1024 : tier.POther);

        if (target.Level != MaxBaseLevel) return (0, 0);
        var (p1024, pOther) = target.Type == RareItemType
            ? (RareUp145P1024, RareUp145POther)
            : (EliteUp145P1024, EliteUp145POther);

        return (146 + target.MartialLevel, materialItemId == 1024 ? p1024 : pOther);
    }

    private static (int EncodedLevel, int Probability) ResolveDownTier(ItemRowDto target, int materialItemId)
    {
        var tiers = target.Type == RareItemType ? RareDownTiers : EliteDownTiers;

        foreach (var tier in tiers)
            if (tier.From == target.Level)
                return (tier.To, materialItemId == 1024 ? tier.P1024 : tier.POther);

        if (target.Level != MaxBaseLevel) return (0, 0);
        if (target.MartialLevel == 0)
        {
            var (to, p1024, pOther) = target.Type == RareItemType
                ? (144, RareDown145Martial0P1024, RareDown145Martial0POther)
                : (142, EliteDown145Martial0P1024, EliteDown145Martial0POther);

            return (to, materialItemId == 1024 ? p1024 : pOther);
        }

        var (p1024Rest, pOtherRest) = target.Type == RareItemType
            ? (RareDown145P1024, RareDown145POther)
            : (EliteDown145P1024, EliteDown145POther);

        return (144 + target.MartialLevel, materialItemId == 1024 ? p1024Rest : pOtherRest);
    }

    /// <summary>
    ///     Verified against every literal case in GetHighMoney/GetLowMoney (function.h:917-1295): a flat
    ///     increment per tier index (100,000 Rare / 1,000,000 Elite), keyed by the target's OWN current level
    ///     (not the destination), with a level-145 martial extension of <paramref name="maxMartialInclusive" />
    ///     entries (11 for High, 12 for Low -- Downgrade can start from any martial tier including the max).
    /// </summary>
    private static int GetTieredMoney(short[] baseLevels, int increment, int maxMartialInclusive, short level,
        byte martialLevel)
    {
        var index = Array.IndexOf(baseLevels, level);
        if (index >= 0) return increment * (index + 1);
        if (level != MaxBaseLevel || martialLevel > maxMartialInclusive)
            return 0;

        index = baseLevels.Length + martialLevel;

        return increment * (index + 1);
    }

    /// <summary>
    ///     Reproduces ReturnForHighItem/ReturnForLowItem (GameSystem_02_Item.cpp:664/733) as a full-catalog scan
    ///     instead of the legacy's boot-time (level,type,sort) bucket index -- same candidate set, same uniform
    ///     random pick, no precomputed index needed. Even a single-candidate match still draws from
    ///     <paramref name="random" /> (matching the legacy's own unconditional <c>rand_mir() % 1</c>).
    /// </summary>
    private static ItemDefinition? FindReplacement(
        ItemDefinition target, int encodedLevel, bool isUpgrade,
        IEnumerable<ItemDefinition> catalog, IRandomSource random)
    {
        if (encodedLevel is < 1 or > MaxBaseLevel + MaxMartialLevel)
            return null;

        int level, martialLevel;
        if (encodedLevel <= MaxBaseLevel)
        {
            level = encodedLevel;
            martialLevel = 0;
        }
        else
        {
            level = MaxBaseLevel;
            martialLevel = encodedLevel - MaxBaseLevel;
        }

        var targetItem = target.Item;
        var targetBonusSkill0 = GetBonusSkillId(target, 0);

        List<ItemDefinition> candidates = [];

        foreach (var candidate in catalog)
        {
            var c = candidate.Item;
            if (c.Level != level || c.MartialLevel != martialLevel)
                continue;
            if (c.Type != targetItem.Type || c.Sort != targetItem.Sort)
                continue;
            if (c.EquipInfo1 != targetItem.EquipInfo1)
                continue;

            if (isUpgrade)
            {
                if (c.CheckHighItem != 2)
                    continue;
            }
            else
            {
                if (c.CheckLowItem != 2)
                    continue;
            }

            if (c.CheckAvatarTrade != targetItem.CheckAvatarTrade)
                continue;
            if (c.CheckMonsterDrop != targetItem.CheckMonsterDrop)
                continue;

            var candidateBonus0 = GetBonusSkillId(candidate, 0);
            var candidateBonus1 = GetBonusSkillId(candidate, 1);
            if (candidateBonus0 != targetBonusSkill0 && candidateBonus1 != targetBonusSkill0)
                continue;

            candidates.Add(candidate);
        }

        return candidates.Count == 0 ? null : candidates[random.NextInt32(candidates.Count)];
    }

    private static int GetBonusSkillId(ItemDefinition definition, byte slot)
    {
        foreach (var bonus in definition.BonusSkills)
            if (bonus.SlotIndex == slot)
                return bonus.SkillId ?? 0;

        return 0;
    }

    /// <summary>
    ///     <see cref="WarlordRerollEligible" /> is set true only on a successful top-tier (encoded 157),
    ///     non-cape upgrade that also passed the 75% rand%4 draw -- the cited signal that the legacy would, at
    ///     this point, attempt a previous-tribe Warlord-set replacement. Fenrir has no Warlord catalog yet, so
    ///     the flag currently drives nothing (the plain <see cref="ResultItemId" /> is granted regardless); it
    ///     exists so a future catalog-backed layer can hook the reroll without re-deriving the decision, and so
    ///     the decision itself is testable today. Always false on the downgrade (LOW) path, which has no reroll.
    /// </summary>
    public readonly record struct RankChangeResult(
        RankChangeOutcome Outcome,
        int Cost,
        int ResultItemId,
        int NewEnchant,
        int NewCombine,
        bool ConsumesLuckyCharge,
        bool WarlordRerollEligible = false);
}
