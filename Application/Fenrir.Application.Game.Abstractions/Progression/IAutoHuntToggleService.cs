using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Progression;

/// <summary>
///     Outcome of <see cref="IAutoHuntToggleService.ToggleAsync" />: <see cref="Aborted" /> means the caller must
///     disconnect the session.
/// </summary>
public readonly record struct AutoHuntToggleResult(bool Aborted, bool Enabled);

public interface IAutoHuntToggleService
{
    public ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken);
}
