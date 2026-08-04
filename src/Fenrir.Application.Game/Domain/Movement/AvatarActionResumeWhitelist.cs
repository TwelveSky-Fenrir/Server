using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Movement;

public static class AvatarActionResumeWhitelist
{
    private static readonly FrozenSet<int> AcceptedSorts = new HashSet<int>
    {
        1, 2, 11, 19, 31, 32, 64, 90, 91, 92, 93, 94, 95
    }.ToFrozenSet();

    private static readonly FrozenSet<int> SortsRequiringZeroType = new HashSet<int> { 19, 31, 64 }.ToFrozenSet();

    private static readonly FrozenSet<int> SortsEndingFishCapture = new HashSet<int> { 94, 95 }.ToFrozenSet();

    public static bool IsLegal(int sort, int type)
    {
        if (!AcceptedSorts.Contains(sort))
            return false;

        return !SortsRequiringZeroType.Contains(sort) || type == 0;
    }

    public static bool EndsFishCapture(int sort)
    {
        return SortsEndingFishCapture.Contains(sort);
    }
}
