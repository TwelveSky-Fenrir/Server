using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public interface ITribeMigrationService
{
    public ValueTask<TribeMigrationOutcome> ConvertAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
