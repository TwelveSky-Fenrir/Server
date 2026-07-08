using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>See <see cref="ITribePopulationService" />.</summary>
public sealed class TribePopulationService(ILogger<TribePopulationService> logger) : ITribePopulationService
{
    public const int TribeCount = 4;

    public IReadOnlyList<int> GetConnectedUserCounts(Zone zone)
    {
        var counts = new int[TribeCount];
        foreach (var player in zone.Players)
            if (player.Tribe < TribeCount)
                counts[player.Tribe]++;

        // Routine per-packet chatter (CZ_GET_ZONE_CONNECT_USER_SEND can be polled frequently by the client
        // UI) -- Debug, not Information, and only formats the array once IsEnabled has already been checked
        // by the LogDebug extension itself.
        logger.LogDebug("Tribe connected-user counts for zone {MapId}: [{Tribe0}, {Tribe1}, {Tribe2}, {Tribe3}]",
            zone.MapId, counts[0], counts[1], counts[2], counts[3]);

        return counts;
    }
}
