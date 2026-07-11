using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Progression;

public readonly record struct AutoHuntToggleResult(bool Aborted, bool Enabled);

public interface IAutoHuntToggleService
{
    public ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken);
}
