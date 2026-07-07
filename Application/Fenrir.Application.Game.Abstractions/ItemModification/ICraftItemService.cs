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

    public ValueTask<CraftFamilyResult> ResolveStoneMatCombineAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveMountFusionAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveWingAssemblyAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveFeatherTierUpAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveWingTierRerollAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveWingFifthTierAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);

    public ValueTask<CraftFamilyResult> ResolveDustRecycleAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);
}

public enum CraftFamilyOutcome
{
    Rejected,
    Applied
}

/// <summary>
///     Shared result shape for the sort 0/2/3/40/41/42/43/45/46/80-84 recipe family (stone-mat combine, mount
///     fusion, wing assembly/tier-up/reroll, wing-dust recycling): slot1 (the "base" material slot) always
///     ends up holding <see cref="ResultItemId" />/<see cref="ResultQuantity" /> (preserving its original
///     Serial via the <c>material1 with { ... }</c> idiom); <see cref="GrantedItem" /> is set only when the
///     recipe additionally grants a brand-new stack into a different free slot because the consumed material
///     only partially depletes (feather tier-up over-quantity, dust-recycle over-threshold).
/// </summary>
public readonly record struct CraftFamilyResult(
    CraftFamilyOutcome Outcome,
    int ResultItemId,
    int ResultQuantity,
    int Serial,
    ItemStack? GrantedItem,
    byte GrantedPage,
    byte GrantedIndex)
{
    public bool Applied => Outcome == CraftFamilyOutcome.Applied;
}
