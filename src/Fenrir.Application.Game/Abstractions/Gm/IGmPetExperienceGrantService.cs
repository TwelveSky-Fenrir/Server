using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmPetExperienceGrantService
{
    public ValueTask HandleAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
