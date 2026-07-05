namespace Fenrir.Application.Game.Social.Pshop;

/// <summary>
///     Posted after a purchase already durably committed, to mirror the sold slot's clearing (and stall
///     close, if last item) onto the seller's live PlayerRuntimeState. Purely a cosmetic mirror -- the
///     purchase's real correctness guard is the live Inventory re-validation under the dual
///     EconomyActionLock -- so this is fire-and-forget, never awaited.
/// </summary>
/// <param name="CharacterId">The seller -- a no-op if they already left this zone.</param>
/// <param name="CloseShop">True when this was the stall's last remaining item -- also clears PshopOpen.</param>
public readonly record struct PshopZoneCommand(int CharacterId, int Page, int Slot, bool CloseShop);
