using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillContributionPointBonuses
{

        public const int RebirthAddValueMultiplier = 2;

        public const int PerUserCrossTribeAddBonus = 1;

        public const int Server160AddedTribeBonus = 2;

        public const int SymbolBattleBaseLevelBonus = 10;

        public const int MinorityCapitalBonus = 10;

        public const int SymbolBattleMinimumBaseLevel = 135;

        public const short SymbolBattleServerId = 38;

        public const short AddedCpTribeServerId = 160;

    private static readonly FrozenSet<short> MinorityCapitalServerIds = new short[] { 1, 6, 11, 140 }.ToFrozenSet();

        public static int ComputeGameWideAddValue(int crossTribeCpAddValueConfig)
    {
        return crossTribeCpAddValueConfig * RebirthAddValueMultiplier;
    }

        public static int ComputePerUserAddValue(bool hasCrossTribeAddTimeEffect)
    {
        return hasCrossTribeAddTimeEffect ? PerUserCrossTribeAddBonus : 0;
    }

        public static int ComputeConditionalBonuses(
        short mapId,
        int attackerTribe,
        int addedCpTribe,
        int attackerBaseLevel,
        bool symbolBattleActive,
        int serverHomeTribe = -1)
    {
        var bonus = 0;

        if (mapId == AddedCpTribeServerId && addedCpTribe >= 0 && attackerTribe == addedCpTribe)
            bonus += Server160AddedTribeBonus;

        if (mapId == SymbolBattleServerId && symbolBattleActive
                                          && attackerBaseLevel >= SymbolBattleMinimumBaseLevel)
            bonus += SymbolBattleBaseLevelBonus;

        if (serverHomeTribe >= 0 && MinorityCapitalServerIds.Contains(mapId) && attackerTribe != serverHomeTribe)
            bonus += MinorityCapitalBonus;

        return bonus;
    }
}
