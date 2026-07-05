using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>See <see cref="ITribePopulationService" />.</summary>
public sealed class TribePopulationService(ZoneRegistry zones) : ITribePopulationService
{
    public const int TribeCount = 4;

    public IReadOnlyList<int> GetConnectedUserCounts()
    {
        var counts = new int[TribeCount];
        for (byte tribe = 0; tribe < TribeCount; tribe++)
        {
            var connectedUsers = 0;
            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                if (player.Tribe == tribe)
                    connectedUsers++;

            counts[tribe] = connectedUsers;
        }

        return counts;
    }
}
