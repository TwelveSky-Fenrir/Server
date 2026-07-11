using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Inventory;

public interface IPetBagActionService
{
    public ValueTask<GenericActionResult> DepositAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> WithdrawAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, CancellationToken cancellationToken);
}
