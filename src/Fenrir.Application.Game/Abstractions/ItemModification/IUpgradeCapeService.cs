using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum UpgradeCapeOutcome
{
    Disconnected,
    Applied
}

public readonly record struct UpgradeCapeResult(UpgradeCapeOutcome Outcome, bool Succeeded, int[] Value);

public interface IUpgradeCapeService
{
    public ValueTask<UpgradeCapeResult> UpgradeAsync(UpgradeCapeRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
