namespace Fenrir.Application.Game.Social.Pshop;

/// <summary>
///     Posted by <c>BuyShopItemHandler</c> after a purchase already durably committed
///     (<c>CharacterRepository.ExecutePshopPurchaseAsync</c>) to mirror the sold slot's clearing (and
///     stall close, if it was the last item) onto the SELLER's live <c>PlayerRuntimeState</c> -- same
///     cross-character rationale as <see cref="Mentor.MentorZoneCommand" />. Purely a cosmetic mirror
///     (the purchase's real correctness guard is the live <c>Inventory</c> re-validation under the dual
///     <c>EconomyActionLock</c>), so this command is fire-and-forget, never awaited.
/// </summary>
/// <param name="CharacterId">The SELLER whose PshopListing this clears -- a no-op if they already left this zone.</param>
/// <param name="Page">The stall page (0-4) whose slot just sold.</param>
/// <param name="Slot">The stall slot (0-4) whose slot just sold.</param>
/// <param name="CloseShop">True when this was the stall's last remaining item -- also clears <c>PshopOpen</c>.</param>
public readonly record struct PshopZoneCommand(int CharacterId, int Page, int Slot, bool CloseShop);
