using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Inventory;

/// <summary>
///     Business logic behind <c>GenericActionHandler</c>'s CZ_PROCESS_DATA_SEND pet-bag-family dispatch: tSort
///     254 (deposit from general inventory), 255 (withdraw to general inventory), 256 (bag-to-bag rearrange).
///     Every method returns the shared <see cref="GenericActionResult" /> so <c>GenericActionHandler</c>'s
///     existing <c>Respond</c> helper handles the wire reply with no new mapping code -- per the C8 behavior
///     contract's own Error/failure semantics section, every rejection in this family is a hard disconnect
///     (<see cref="GenericActionStatus.Aborted" />), never a soft/clean failure reply.
/// </summary>
/// <remarks>
///     Réf. contract: C8-hotkey-money-petbag (legacy-behavior-translator, wave7) -- see
///     <c>Fenrir.Application.Game.Domain.Inventory.PetBagItemTransferPolicy</c>'s own citation trail for the
///     underlying <c>Server/ts25zone/S04_MyWork05.cpp</c> ranges this orchestrates. The three
///     entitlement/precondition bools each method takes (pet equipped, pet-bag upper-half rented window,
///     second-inventory-page rented window) are caller-resolved, same posture as the policy's own inputs --
///     <c>PlayerRuntimeState</c> already carries the entitlement dates
///     (<c>PlayerRuntimeState.PetBagUpperHalfEntitlementExpiryUtc</c>-equivalent is NOT modeled yet; see the C8
///     wiring manifest's own open question) so callers pass a conservative default until that lands.
/// </remarks>
public interface IPetBagActionService
{
    /// <summary>
    ///     tSort 254 -- <paramref name="move" />: Page1/Index1 = source general-inventory page/slot, Page2 =
    ///     destination pet-bag slot (the dispatcher's own field re-map, per the C8 contract's Inputs section --
    ///     Index2/XPost2/YPost2 are ignored on this direction).
    /// </summary>
    public ValueTask<GenericActionResult> DepositAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 255 -- <paramref name="move" />: Index1 = source pet-bag slot, Quantity1 = destination
    ///     inventory page, Page2 = destination inventory slot, Index2 = destination cell X, XPost2 =
    ///     destination cell Y (the dispatcher's own field re-map, per the C8 contract's Inputs section).
    /// </summary>
    public ValueTask<GenericActionResult> WithdrawAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 256 -- <paramref name="move" />: Index1 = source pet-bag slot, Page2 = destination pet-bag
    ///     slot (the dispatcher's own field re-map). A source-equals-destination request is a bare
    ///     no-mutation success -- neither the database nor the zone tick is touched.
    /// </summary>
    public ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool petBagUpperHalfEntitlementActive, CancellationToken cancellationToken);
}
