using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Progression;

public static class LevelMilestoneBonus
{
    public const int LvM2 = 114;
    public const int LvM8 = 120;
    public const int LvM14 = 126;
    public const int LvM20 = 132;
    public const int LvM26 = 138;
    public const int LvM32 = 144;
    public const int LvM33 = 145;

        public static readonly FrozenSet<int> ArmableMilestoneLevels =
        new[] { 45, 65, 85, 105, LvM2, LvM8, LvM14, LvM20, LvM26, LvM32, LvM33 }.ToFrozenSet();

        public static readonly FrozenSet<int> DeferredMilestoneLevels = FrozenSet<int>.Empty;

    private static readonly int[] ArmableMilestoneLevelsAscending =
        [45, 65, 85, 105, LvM2, LvM8, LvM14, LvM20, LvM26, LvM32, LvM33];

        public static bool IsArmableMilestone(int level)
    {
        return ArmableMilestoneLevels.Contains(level);
    }

        public static int ResolveHighestMilestoneCrossed(int previousLevel, int newLevel)
    {
        var highest = 0;
        foreach (var milestone in ArmableMilestoneLevelsAscending)
            if (milestone > previousLevel && milestone <= newLevel)
                highest = milestone;

        return highest;
    }

        public static bool TryResolveClaimDrops(int bonusItemLevel, byte previousTribe,
        out ImmutableArray<TribeGroundItemDrop> drops)
    {
        switch (bonusItemLevel)
        {
            case 45:
                drops = [new TribeGroundItemDrop(99700, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 65:
                drops = [new TribeGroundItemDrop(99701, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 85:
                drops = [new TribeGroundItemDrop(99702, 1), new TribeGroundItemDrop(539, 1)];
                return true;
            case 105:
                drops = [new TribeGroundItemDrop(845, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case LvM2:
                drops = [new TribeGroundItemDrop(847, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case LvM8:
                drops = [new TribeGroundItemDrop(846, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case LvM14:
                drops = [new TribeGroundItemDrop(848, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case LvM20:
                drops =
                [
                    new TribeGroundItemDrop(850, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case LvM26:
                drops =
                [
                    new TribeGroundItemDrop(99699, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case LvM32:
                drops =
                [
                    new TribeGroundItemDrop(99698, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case LvM33:
                drops = BuildM33ClaimDrops(previousTribe);
                return true;
            default:
                drops = default;
                return false;
        }
    }

    private static ImmutableArray<TribeGroundItemDrop> BuildM33ClaimDrops(byte previousTribe)
    {
        var tribeItemId = previousTribe switch
        {
            0 => 83809,
            1 => 83857,
            2 => 83906,
            _ => 0
        };

        var builder = ImmutableArray.CreateBuilder<TribeGroundItemDrop>(tribeItemId == 0 ? 4 : 5);
        builder.Add(new TribeGroundItemDrop(851, 1));
        builder.Add(new TribeGroundItemDrop(1022, 10));
        builder.Add(new TribeGroundItemDrop(1023, 10));
        builder.Add(new TribeGroundItemDrop(1019, 10));

        if (tribeItemId != 0)
            builder.Add(new TribeGroundItemDrop(tribeItemId, 1));

        return builder.ToImmutable();
    }
}
