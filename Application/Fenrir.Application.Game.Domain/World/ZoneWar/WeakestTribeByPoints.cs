using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class WeakestTribeByPoints
{

        public static byte Resolve(IReadOnlyList<int> tribePoints)
    {
        ArgumentNullException.ThrowIfNull(tribePoints);
        if (tribePoints.Count != WorldStateService.TribeCount)
            throw new ArgumentException(
                $"Expected exactly {WorldStateService.TribeCount} tribe point totals.", nameof(tribePoints));

        byte weakest = 0;
        for (byte i = 1; i < WorldStateService.TribeCount; i++)
            if (tribePoints[i] < tribePoints[weakest])
                weakest = i;

        return weakest;
    }

        public static byte Resolve(WorldStateService worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        var points = new int[WorldStateService.TribeCount];
        for (byte i = 0; i < WorldStateService.TribeCount; i++)
            points[i] = worldState.GetTribe(i).Points;

        return Resolve(points);
    }
}
