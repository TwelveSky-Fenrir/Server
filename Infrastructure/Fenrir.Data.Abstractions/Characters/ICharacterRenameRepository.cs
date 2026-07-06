namespace Fenrir.Data.Abstractions.Characters;

// Split from CharacterRepository (parallel workstream); interface so RenameAvatarHandler is unit-testable without SQL.
public interface ICharacterRenameRepository
{
    /// <summary>
    ///     Atomically re-verifies the item at (<paramref name="itemContainer" />, <paramref name="itemSlot" />) is
    ///     still item 1133 (the rename scroll), re-checks the new name isn't already taken by any character, and
    ///     -- only if both hold -- deletes that item slot and renames the character, all in one transaction.
    ///     Nothing is mutated on any failure path (this is a genuine improvement over the legacy's manual,
    ///     non-transactional two-step mutation/compensation -- see RenameAvatarService's own remarks for the
    ///     citation of the bug this sidesteps).
    /// </summary>
    /// <returns>
    ///     Legacy mDB.ChangeCharacterName codes, forwarded verbatim: 0 renamed, 2 name taken, 102 no character at
    ///     that slot. -1 is a Fenrir-internal sentinel (never a legacy wire code) for the rare race where the
    ///     item at the claimed slot no longer matches by the time this call runs, even though
    ///     RenameAvatarService already checked it moments earlier -- the caller must treat -1 exactly like an
    ///     item-gate failure (session disconnect, no response). Exceptions map to legacy 101 by the caller.
    /// </returns>
    public ValueTask<int> RenameAndConsumeItemAsync(int accountId, byte slot, string newName, byte itemContainer,
        byte itemSlot, CancellationToken ct);
}
