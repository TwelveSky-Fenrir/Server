using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum JadeUpgradeOutcome
{
    Rejected,
    Applied
}

public readonly record struct JadeUpgradeResult(JadeUpgradeOutcome Outcome, int ResultItemId, int Serial);

public enum AdvancedElixirOutcome
{
    Rejected,
    Success,
    Failed
}

public readonly record struct AdvancedElixirResult(
    AdvancedElixirOutcome Outcome,
    ItemStack? NewItemStack,
    byte ResultPage,
    byte ResultIndex,
    ItemStack? RemainingMaterial);

public interface ICraftItemService
{
    public ValueTask<JadeUpgradeResult> ResolveJadeUpgradeAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state,
        int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<AdvancedElixirResult> ResolveAdvancedElixirAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);
}
