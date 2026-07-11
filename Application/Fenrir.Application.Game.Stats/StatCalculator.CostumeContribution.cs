using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{

        public static readonly FrozenSet<int> ValidCostumeIds = BuildValidCostumeIds();

    private static FrozenSet<int> BuildValidCostumeIds()
    {
        var ids = new HashSet<int>();
        AddInclusiveRange(ids, 301, 402);
        AddInclusiveRange(ids, 2146, 2148);
        AddInclusiveRange(ids, 1801, 1803);
        AddInclusiveRange(ids, 1891, 1893);
        AddInclusiveRange(ids, 17701, 17703);
        AddInclusiveRange(ids, 18124, 18132);
        AddInclusiveRange(ids, 93301, 93315);
        AddInclusiveRange(ids, 93316, 93317);
        AddInclusiveRange(ids, 93318, 93330);
        AddInclusiveRange(ids, 93334, 93345);
        AddInclusiveRange(ids, 93376, 93381);
        AddInclusiveRange(ids, 93385, 93387);
        AddInclusiveRange(ids, 93388, 93405);
        AddInclusiveRange(ids, 76524, 76526);
        return ids.ToFrozenSet();
    }

    private static void AddInclusiveRange(HashSet<int> set, int lo, int hi)
    {
        for (var id = lo; id <= hi; id++)
            set.Add(id);
    }

        public static bool IsValidCostume(int costumeId)
    {
        return ValidCostumeIds.Contains(costumeId);
    }

        public static bool IsDecoStatCostume(int costumeId)
    {
        return IsCustomDecoStatItem(costumeId);
    }


        public static CostumeBaseStatBlock ComputeCostumeBaseStatBlock(
        int costumeId,
        bool itemFound,
        int itemVitality,
        int itemStrength,
        int itemIntelligence,
        int itemDexterity)
    {
        if (!itemFound)
            return default;

        var vitality = itemVitality;
        var strength = itemStrength;
        var intelligence = itemIntelligence;
        var dexterity = itemDexterity;

        if (IsDecoStatCostume(costumeId))
        {
            if (vitality < 100) vitality = 100;
            if (strength < 100) strength = 100;
            if (intelligence < 100) intelligence = 100;
            if (dexterity < 100) dexterity = 100;
        }

        if (IsValidCostume(costumeId))
        {
            vitality += 100;
            strength += 100;
            intelligence += 100;
            dexterity += 100;
        }

        return new CostumeBaseStatBlock(vitality, strength, intelligence, dexterity);
    }


        public static int DecodeCostumeEnchantCs(int costumeIndex, ReadOnlySpan<int> costumeDate)
    {
        if (costumeIndex is < 10 or > 19)
            return 0;

        var slot = costumeIndex % 10;
        if ((uint)slot >= (uint)costumeDate.Length)
            return 0;

        return ReadSignedLowByte(costumeDate[slot]);
    }

        public static int ReadSignedLowByte(int packedWord)
    {
        return (sbyte)(packedWord & 0xFF);
    }


        public static int CostumeVitalityContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Vitality + cs;
    }

        public static int CostumeKiContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Intelligence + cs;
    }

        public static int CostumeStrengthContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Strength + cs;
    }

        public static int CostumeWisdomContribution(CostumeBaseStatBlock block, int cs)
    {
        return block.Dexterity + cs;
    }

        public static int CostumeCriticalContribution(int cs)
    {
        return cs == 96 ? 10 : cs > 0 ? cs / 10 : 0;
    }

        public static int CostumeLuckContribution(int costumeId, int cs)
    {
        return cs * 2 + (IsValidCostume(costumeId) ? 100 : 0);
    }
}

public readonly record struct CostumeBaseStatBlock(int Vitality, int Strength, int Intelligence, int Dexterity);
