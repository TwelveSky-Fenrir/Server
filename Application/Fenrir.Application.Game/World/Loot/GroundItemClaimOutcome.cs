namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Result of <see cref="Zone.TryClaimGroundItem" /> -- every non-success value is a soft "no" (matches legacy's
///     own <c>ProcessForGetItem</c>'s <c>return TRUE</c> with <c>tResult</c> left at 1, never a disconnect).
/// </summary>
public enum GroundItemClaimOutcome
{
    /// <summary>No such item (unique-number mismatch, already gone, or lost the race to a concurrent claimant).</summary>
    NotFound,

    /// <summary>Ownership window not yet open for this claimant (killer first, then party, then free-for-all).</summary>
    NotOwned,

    /// <summary>Farther than <see cref="Inventory.GroundItemPickupPolicy.MaxPickupDistance" /> from the item.</summary>
    TooFar,

    Success
}
