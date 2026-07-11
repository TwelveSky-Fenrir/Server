using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Inventory;

/// <summary>
///     game.CharacterPetBag access -- the character-scoped 20-slot pet/animal cargo bag (legacy
///     <c>aPetBagDate</c>-adjacent array, Server/Header/Protocol/STRUCT.h:520-521), backing
///     CZ_PROCESS_DATA_SEND tSort 254 (deposit from general inventory)/255 (withdraw to general inventory)/256
///     (bag-to-bag rearrange). A brand-new NEW dedicated interface rather than an addition to
///     <see cref="ICharacterRepository" /> -- see the C8 (wave7) wiring manifest for why: this pass could not
///     edit that already-large interface directly (concurrent-agent constraint), and a small, single-purpose
///     repository mirrors the same separation <see cref="Fenrir.Data.Abstractions.Accounts.IAccountVaultRepository" />
///     already establishes for its own account-scoped concern.
/// </summary>
public interface IPetBagRepository
{
    /// <summary>
    ///     tSort 254 -- atomically clears one general-inventory slot (whole-container replace, same
    ///     empty-list-omission rule as <see cref="ICharacterRepository.ReplaceContainerAsync" />) and occupies
    ///     one pet-bag slot. Throws on an already-occupied destination slot (re-verified server-side against a
    ///     concurrent request).
    /// </summary>
    public ValueTask DepositAsync(int characterId, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, byte petBagSlot, int petItemId, CancellationToken ct);

    /// <summary>
    ///     tSort 255 -- atomically clears one pet-bag slot and whole-replaces the destination general-inventory
    ///     container with the caller's already-resolved post-withdraw content.
    /// </summary>
    public ValueTask WithdrawAsync(int characterId, byte petBagSlot, byte inventoryContainer,
        IReadOnlyList<CharacterItemSlotTvp> inventoryItems, CancellationToken ct);

    /// <summary>
    ///     tSort 256 -- atomically moves one pet-bag slot's content into another (source cleared, destination
    ///     set). Throws if the source is empty or the destination is already occupied (re-verified
    ///     server-side). The caller is expected to short-circuit a source-equals-destination request as a
    ///     no-op before ever calling this, matching <c>PetBagItemTransferPolicy.TransferOutcome.NoOp</c>'s own
    ///     posture.
    /// </summary>
    public ValueTask RearrangeAsync(int characterId, byte sourceSlot, byte destinationSlot, CancellationToken ct);
}
