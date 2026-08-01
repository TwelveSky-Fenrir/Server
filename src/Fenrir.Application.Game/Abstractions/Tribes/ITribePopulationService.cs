using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public interface ITribePopulationService
{
    public IReadOnlyList<int> GetConnectedUserCounts(Zone zone);
}
