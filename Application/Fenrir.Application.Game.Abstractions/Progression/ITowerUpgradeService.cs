using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum TowerUpgradeOutcome
{
    /// <summary>Any validation/material/persistence gate failed -- caller must disconnect.</summary>
    Aborted,

    Success
}

/// <summary>
///     <see cref="PackedPage" />/<see cref="PackedIndex" /> only meaningful when <see cref="Outcome" /> is
///     <see cref="TowerUpgradeOutcome.Success" />.
/// </summary>
public readonly record struct TowerUpgradeResult(TowerUpgradeOutcome Outcome, int PackedPage, int PackedIndex);

public interface ITowerUpgradeService
{
    public ValueTask<TowerUpgradeResult> UpgradeAsync(int characterId, Zone zone, PlayerRuntimeState state,
        TowerUpgradeRequest packet, CancellationToken cancellationToken);
}
