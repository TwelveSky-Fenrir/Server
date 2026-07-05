using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Tribes.Services;

/// <summary>Business logic behind CZ_GET_ZONE_CONNECT_USER_SEND (opcode 92), extracted out of <see cref="TribePopulationHandler" />.</summary>
public interface ITribePopulationService
{
    /// <summary>Live connected-player count for every tribe (0-3), across every zone of this process.</summary>
    IReadOnlyList<int> GetConnectedUserCounts();
}

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
