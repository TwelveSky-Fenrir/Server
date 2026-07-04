namespace Fenrir.Application.Game.Social.Pshop;

/// <summary>
///     Posted by <c>BuyShopItemHandler</c> AFTER a live PShop purchase already durably committed
///     (<c>CharacterRepository.ExecutePshopPurchaseAsync</c>) to mirror the SOLD slot's clearing (and,
///     when the stall just sold its last item, the stall's own close) onto the SELLER's live
///     <c>Fenrir.Application.Game.World.PlayerRuntimeState</c> -- the cross-character twin of
///     <see cref="Mentor.MentorZoneCommand" />, same rationale: the buyer's own request thread must never
///     write directly onto a DIFFERENT (the seller's) character's live state, even under both
///     participants' <c>PlayerRuntimeState.EconomyActionLock</c> -- see
///     <c>PlayerRuntimeState.PshopOpen</c>'s own remarks for why this is purely a COSMETIC mirror (the
///     purchase's actual correctness guard is the live <c>Inventory</c> re-validation under the dual lock,
///     never this cached display copy), so this command is fire-and-forget, never awaited.
/// </summary>
/// <param name="CharacterId">The SELLER whose PshopListing this clears -- a no-op if they already left this zone.</param>
/// <param name="Page">The stall page (0-4) whose slot just sold.</param>
/// <param name="Slot">The stall slot (0-4) whose slot just sold.</param>
/// <param name="CloseShop">True when this was the stall's last remaining item -- also clears <c>PshopOpen</c>.</param>
public readonly record struct PshopZoneCommand(int CharacterId, int Page, int Slot, bool CloseShop);
