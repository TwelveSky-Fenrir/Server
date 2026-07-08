using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum SkyUpgradeItemOutcome
{
    Rejected,
    Applied
}

public readonly record struct SkyUpgradeItemResult(SkyUpgradeItemOutcome Outcome, bool Succeeded, int[] Value);

public interface ISkyUpgradeItemService
{
    public ValueTask<SkyUpgradeItemResult> UpgradeAsync(SkyUpgradeItemRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
