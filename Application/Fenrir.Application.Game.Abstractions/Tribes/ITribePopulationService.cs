using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>
///     Business logic behind CZ_GET_ZONE_CONNECT_USER_SEND (opcode 92), extracted out of
///     <see cref="TribePopulationHandler" />.
/// </summary>
public interface ITribePopulationService
{
    /// <summary>
    ///     Live connected-player count for every tribe (0-3), scoped to <paramref name="zone" /> only -- the
    ///     single map the requester is currently on, matching legacy one-process-per-map semantics.
    /// </summary>
    public IReadOnlyList<int> GetConnectedUserCounts(Zone zone);
}
