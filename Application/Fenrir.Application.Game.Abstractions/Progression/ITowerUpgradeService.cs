using Fenrir.Application.Game.Domain.Inventory;
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

    /// <summary>
    ///     A11 -- item-665 from-scratch tower construction, reached from
    ///     <c>UseInventoryItemService.ResolveAsync</c>'s own item-id dispatch (a direct if-cascade branch, NOT the
    ///     C9 <c>UseItemHandlerRegistry</c> -- that registry is a distinct, synchronous per-item-category dispatch
    ///     for a disjoint family of items; see this feature's wiring manifest). <paramref name="constructType" />
    ///     is the use-item request's numeric value (1=Silver/2=CP/3=EXP) passed through unmodified. Every
    ///     rejection returns a Result=1 <see cref="UseInventoryItemResponse" /> with the item retained; the item is
    ///     consumed only on success (Result=0).
    /// </summary>
    public ValueTask<UseInventoryItemResponse> ConstructAsync(int characterId, Zone zone, PlayerRuntimeState state,
        byte page, byte index, ItemStack item, int constructType, CancellationToken cancellationToken);

    /// <summary>
    ///     A11 -- item-667 tower-guardian heal (+10% of max life, within 200 units of the tower), reached from
    ///     <c>UseInventoryItemService.ResolveAsync</c>'s own item-id dispatch (see <see cref="ConstructAsync" />'s
    ///     own remarks on why this is not the C9 registry). Consumes the item and arms the tick-side heal on
    ///     success (Result=0); every rejection returns Result=1 with the item retained.
    /// </summary>
    public ValueTask<UseInventoryItemResponse> HealAsync(int characterId, Zone zone, PlayerRuntimeState state,
        byte page, byte index, ItemStack item, CancellationToken cancellationToken);
}
