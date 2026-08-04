using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Progression;

public static class LevelMilestoneBonus
{
    public const int Level114 = 114;
    public const int Level120 = 120;
    public const int Level126 = 126;
    public const int Level132 = 132;
    public const int Level138 = 138;
    public const int Level144 = 144;
    public const int MaximumLevel = 145;

    public static readonly FrozenSet<int> ArmableMilestoneLevels =
        new[] { 45, 65, 85, 105, Level114, Level120, Level126, Level132, Level138, Level144, MaximumLevel }
            .ToFrozenSet();

    public static readonly FrozenSet<int> DeferredMilestoneLevels = FrozenSet<int>.Empty;

    private static readonly int[] ArmableMilestoneLevelsAscending =
        [45, 65, 85, 105, Level114, Level120, Level126, Level132, Level138, Level144, MaximumLevel];

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
            case Level114:
                drops = [new TribeGroundItemDrop(847, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case Level120:
                drops = [new TribeGroundItemDrop(846, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case Level126:
                drops = [new TribeGroundItemDrop(848, 1), new TribeGroundItemDrop(539, 2)];
                return true;
            case Level132:
                drops =
                [
                    new TribeGroundItemDrop(850, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case Level138:
                drops =
                [
                    new TribeGroundItemDrop(99699, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case Level144:
                drops =
                [
                    new TribeGroundItemDrop(99698, 1), new TribeGroundItemDrop(539, 2),
                    new TribeGroundItemDrop(1458, 1)
                ];
                return true;
            case MaximumLevel:
                drops = BuildMaximumLevelClaimDrops(previousTribe);
                return true;
            default:
                drops = default;
                return false;
        }
    }

    private static ImmutableArray<TribeGroundItemDrop> BuildMaximumLevelClaimDrops(byte previousTribe)
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
