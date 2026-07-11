using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{

        public const int FourGuildSlotNone = -1;

        private const int EventDisabledTribe = -1;

        private const int AllTribesEventTribeSentinel = 4;

        private const int EventTier10CountCap = 100;

        private const int EventTier20CountCap = 200;

        private const int DexterityElixirRate = 2;

        private static readonly FrozenDictionary<FourGuildElixirStat, FourGuildElixirParams> FourGuildElixirTable =
        BuildFourGuildElixirTable();

    private static FrozenDictionary<FourGuildElixirStat, FourGuildElixirParams> BuildFourGuildElixirTable()
    {
        var hpSlots = new[] { 0, 1 }.ToFrozenSet();
        var mpSlots = new[] { 0, 1, 2, 3 }.ToFrozenSet();
        var damageSlots = new[] { 0 }.ToFrozenSet();
        var accuracyBlockSlots = new[] { 0, 1, 2 }.ToFrozenSet();

        return new Dictionary<FourGuildElixirStat, FourGuildElixirParams>
        {
            [FourGuildElixirStat.Life] = new(4000, hpSlots, 2000, 4000, 6000, LifeElixirRate),
            [FourGuildElixirStat.Mana] = new(5000, mpSlots, 2500, 5000, 7500, ManaElixirRate),
            [FourGuildElixirStat.Damage] = new(600, damageSlots, 300, 600, 900, StrengthElixirAttackRate),
            [FourGuildElixirStat.Accuracy] =
                new(400, accuracyBlockSlots, 200, 400, 600, DexterityElixirRate),
            [FourGuildElixirStat.Block] = new(400, accuracyBlockSlots, 200, 400, 600, DexterityElixirRate)
        }.ToFrozenDictionary();
    }

        private static int ComputeFourGuildElixirDelta(
        FourGuildElixirStat stat,
        int potionCount,
        int b4gFlag,
        int fourGuildSlot,
        int avatarTribe,
        int eventTribe,
        int eventTier)
    {
        var p = FourGuildElixirTable[stat];

        if (b4gFlag == 1 && p.FixedOverrideSlots.Contains(fourGuildSlot))
            return p.FixedOverride;

        var eventMatches = eventTribe >= 0 &&
                           (eventTribe == avatarTribe || eventTribe == AllTribesEventTribeSentinel);
        if (eventMatches)
            return eventTier switch
            {
                10 => potionCount < EventTier10CountCap ? p.EventTier10Bonus : p.RawRatePerPotion * potionCount,
                20 => potionCount < EventTier20CountCap ? p.EventTier20Bonus : p.RawRatePerPotion * potionCount,
                30 => p.EventTier30Bonus,
                _ => p.RawRatePerPotion * potionCount
            };

        return p.RawRatePerPotion * potionCount;
    }

        public static int LifeElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int hpElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Life, consumable.EatLifePotion,
            hpElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

        public static int ManaElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int mpElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Mana, consumable.EatManaPotion,
            mpElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

        public static int StrengthElixirAttackContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int strElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Damage, consumable.EatStrPotion,
            strElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

        public static int AccuracyElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int dexElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Accuracy, consumable.EatDexPotion,
            dexElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

        public static int BlockElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int dexElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Block, consumable.EatDexPotion,
            dexElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

        public static int ResolveFourGuildSlot(
        int avatarTribe,
        ReadOnlySpan<char> guildName,
        ReadOnlySpan<string> tribeFourGuildNames)
    {
        if (avatarTribe is < 0 or >= AllTribesEventTribeSentinel)
            return FourGuildSlotNone;
        if (guildName.IsEmpty)
            return FourGuildSlotNone;

        var count = Math.Min(tribeFourGuildNames.Length, AllTribesEventTribeSentinel);
        for (var slot = 0; slot < count; slot++)
        {
            var designated = tribeFourGuildNames[slot];
            if (!string.IsNullOrEmpty(designated) && guildName.SequenceEqual(designated))
                return slot;
        }

        return FourGuildSlotNone;
    }

        private enum FourGuildElixirStat : byte
    {
        Life = 0,
        Mana = 1,
        Damage = 2,
        Accuracy = 3,
        Block = 4
    }

        private readonly record struct FourGuildElixirParams(
        int FixedOverride,
        FrozenSet<int> FixedOverrideSlots,
        int EventTier10Bonus,
        int EventTier20Bonus,
        int EventTier30Bonus,
        int RawRatePerPotion);
}
