using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

public interface IGmPetExperienceGrantService
{
    public ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
